using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GymBro.Tests.Integration
{
    /// <summary>
    /// Etapa 7d: protocolo de piernas contra la API real (PostgreSQL descartable, nunca gymbro-db). Escenario SINTÉTICO:
    /// 10 ejercicios de prueba con la forma de la siembra piernas-1.0 (la siembra real solo encuentra los UUID de la base
    /// del autor), más un ejercicio "Legs" del dataset que no está en el protocolo. Cada prueba crea su propio socio para no
    /// depender del orden de ejecución.
    /// </summary>
    public class ProtocoloPiernasIntegracionTests : IClassFixture<ProtocoloPiernasIntegracionTests.Escenario>
    {
        private readonly Escenario _e;
        public ProtocoloPiernasIntegracionTests(Escenario e) { _e = e; }

        private HttpClient Como(Usuario u) => _e.Api.ClienteComo(u);

        private static async Task<JsonElement> Json(HttpResponseMessage r, HttpStatusCode esperado)
        {
            var texto = await r.Content.ReadAsStringAsync();
            Assert.True(r.StatusCode == esperado, $"{(int)r.StatusCode}: {texto}");
            return JsonDocument.Parse(texto).RootElement.Clone();
        }

        private async Task<JsonElement> GenerarAsync(Usuario quien, Guid socioId) =>
            await Json(await Como(quien).PostAsync($"/api/Socio/{socioId}/prescripciones/piernas", null), HttpStatusCode.Created);

        private static List<Guid> Ejercicios(JsonElement rutina) =>
            rutina.GetProperty("ejercicios").EnumerateArray().Select(e => e.GetProperty("ejercicioId").GetGuid()).ToList();

        // ---- CA1, CA2, CA6 ----

        [Fact]
        public async Task Genera_seis_ejercicios_del_protocolo_con_trazabilidad_y_rota_la_variable_en_la_siguiente_generacion()
        {
            var (socio, cuenta) = await _e.NuevoSocioIndependienteAsync();

            var primera = await GenerarAsync(cuenta, socio.Id);
            Assert.Equal("Aprobada", primera.GetProperty("estado").GetString());       // independiente: sin revisión
            Assert.Equal(1, primera.GetProperty("numeroGeneracion").GetInt32());
            Assert.Equal("piernas-1.0", primera.GetProperty("protocoloVersion").GetString());
            var ids = Ejercicios(primera);
            Assert.Equal(6, ids.Count);
            Assert.All(ids, id => Assert.Contains(id, _e.Protocolo.Values));
            Assert.DoesNotContain(_e.LegsDataset, ids);
            Assert.Equal(ids.Count, ids.Distinct().Count());
            Assert.Equal(_e.Protocolo["Trasera"], ids[2]);
            Assert.All(primera.GetProperty("ejercicios").EnumerateArray(), e =>
            {
                Assert.Equal(3, e.GetProperty("tempoExcentricoSeg").GetInt32());
                Assert.False(string.IsNullOrEmpty(e.GetProperty("rol").GetString()));
                Assert.True(e.GetProperty("rirObjetivo").GetInt32() >= 1);
            });

            var segunda = await GenerarAsync(cuenta, socio.Id);
            Assert.Equal(2, segunda.GetProperty("numeroGeneracion").GetInt32());
            Assert.Equal(_e.Protocolo["Frontal"], Ejercicios(segunda)[2]);             // (2 - 1) mod 3 = 1

            // Persistencia (CA6): versión, número, origen copiado, snapshot, rol, tempo y motivo por línea.
            PrescripcionEntrenamiento? guardada = null;
            await _e.Api.ConDbAsync(async db => guardada = await db.PrescripcionesEntrenamiento.Include(p => p.EjerciciosPrescritos)
                .SingleAsync(p => p.Id == primera.GetProperty("id").GetGuid()));
            Assert.Equal(("PIERNAS", "piernas-1.0", 1, OrigenDatos.Real), (guardada!.ProtocoloCodigo, guardada.ProtocoloVersion,
                guardada.NumeroGeneracion!.Value, guardada.OrigenDatos));
            Assert.Equal("PROTOCOLO_PIERNAS_DETERMINISTA", guardada.OrigenGeneracion);
            Assert.NotNull(guardada.EntradaSnapshot);
            Assert.DoesNotContain(socio.Email, guardada.EntradaSnapshot);
            Assert.All(guardada.EjerciciosPrescritos, l =>
            {
                Assert.NotNull(l.Rol);
                Assert.NotNull(l.Grupo);
                Assert.Equal((short)3, l.TempoExcentricoSeg);
                Assert.Contains("piernas-1.0", l.MotivoSeleccion);
            });

            // El listado (GET) devuelve los campos aditivos.
            var lista = await Json(await Como(cuenta).GetAsync($"/api/Socio/{socio.Id}/prescripciones"), HttpStatusCode.OK);
            Assert.Equal(2, lista.GetArrayLength());
            Assert.Equal("Referencia", lista[0].GetProperty("ejercicios")[0].GetProperty("rol").GetString());
        }

        [Fact]
        public async Task Origen_sintetico_del_socio_se_copia_a_la_prescripcion()
        {
            var (socio, cuenta) = await _e.NuevoSocioIndependienteAsync(OrigenDatos.Sintetico);
            var rutina = await GenerarAsync(cuenta, socio.Id);
            OrigenDatos origen = default;
            await _e.Api.ConDbAsync(async db => origen = (await db.PrescripcionesEntrenamiento.SingleAsync(p => p.Id == rutina.GetProperty("id").GetGuid())).OrigenDatos);
            Assert.Equal(OrigenDatos.Sintetico, origen);
        }

        // ---- CA3: limitaciones ----

        [Fact]
        public async Task Las_limitaciones_se_guardan_y_excluyen_ejercicios_con_el_motivo_registrado()
        {
            var (socio, cuenta) = await _e.NuevoSocioIndependienteAsync();
            var inicial = await Json(await Como(cuenta).GetAsync($"/api/Socio/{socio.Id}/limitaciones-articulares"), HttpStatusCode.OK);
            Assert.Equal(("SinLimitacion", "SinLimitacion"), (inicial.GetProperty("rodilla").GetString(), inicial.GetProperty("lumbar").GetString()));

            var guardado = await Json(await Como(cuenta).PutAsJsonAsync($"/api/Socio/{socio.Id}/limitaciones-articulares",
                new { rodilla = "Moderada", lumbar = "alta" }), HttpStatusCode.OK);
            Assert.Equal(("Moderada", "Alta"), (guardado.GetProperty("rodilla").GetString(), guardado.GetProperty("lumbar").GetString()));

            var rutina = await GenerarAsync(cuenta, socio.Id);
            var ids = Ejercicios(rutina);
            // Lumbar Alta: sin Prensa (posición omitida), sin sentadillas trasera/frontal ni peso muerto; rodilla Moderada: sin extensión.
            Assert.DoesNotContain(_e.Protocolo["Prensa"], ids);
            Assert.DoesNotContain(_e.Protocolo["Extension"], ids);
            Assert.DoesNotContain(_e.Protocolo["Trasera"], ids);
            Assert.DoesNotContain(_e.Protocolo["PesoMuerto"], ids);
            Assert.Contains("Posiciones omitidas", rutina.GetProperty("observaciones").GetString());

            string? motivoHack = null;
            await _e.Api.ConDbAsync(async db => motivoHack = (await db.PrescripcionesEjercicios
                .SingleAsync(l => l.PrescripcionEntrenamientoId == rutina.GetProperty("id").GetGuid() && l.EjercicioId == _e.Protocolo["Hack"])).MotivoSeleccion);
            Assert.NotNull(motivoHack);

            // Con rodilla Alta no hay ejercicios de cuádriceps elegibles: 400 indicando el grupo.
            await Json(await Como(cuenta).PutAsJsonAsync($"/api/Socio/{socio.Id}/limitaciones-articulares",
                new { rodilla = "Alta", lumbar = "SinLimitacion" }), HttpStatusCode.OK);
            var r = await Como(cuenta).PostAsync($"/api/Socio/{socio.Id}/prescripciones/piernas", null);
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
            Assert.Contains("Cuádriceps", await r.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task Las_limitaciones_exigen_consentimiento_y_valores_validos()
        {
            var (socio, cuenta) = await _e.NuevoSocioIndependienteAsync(consentimiento: false);
            Assert.Equal(HttpStatusCode.BadRequest, (await Como(cuenta).PutAsJsonAsync($"/api/Socio/{socio.Id}/limitaciones-articulares",
                new { rodilla = "Moderada", lumbar = "SinLimitacion" })).StatusCode);

            var (otro, cuentaOtro) = await _e.NuevoSocioIndependienteAsync();
            Assert.Equal(HttpStatusCode.BadRequest, (await Como(cuentaOtro).PutAsJsonAsync($"/api/Socio/{otro.Id}/limitaciones-articulares",
                new { rodilla = "2", lumbar = "SinLimitacion" })).StatusCode);

            var auditadas = 0;
            await _e.Api.ConDbAsync(async db => auditadas = await db.AuditLogs.CountAsync(a => a.Accion == "LIMITACIONES_ARTICULARES" && a.EntidadId == otro.Id.ToString()));
            Assert.Equal(0, auditadas);
            await Json(await Como(cuentaOtro).PutAsJsonAsync($"/api/Socio/{otro.Id}/limitaciones-articulares",
                new { rodilla = "Moderada", lumbar = "SinLimitacion" }), HttpStatusCode.OK);
            AuditLog? log = null;
            await _e.Api.ConDbAsync(async db => log = await db.AuditLogs.SingleAsync(a => a.Accion == "LIMITACIONES_ARTICULARES" && a.EntidadId == otro.Id.ToString()));
            Assert.Null(log!.Detalle);   // la auditoría no guarda los valores (dato de salud)
        }

        // ---- CA7: vínculo sesión -> línea de prescripción ----

        [Fact]
        public async Task La_sesion_guarda_el_vinculo_con_la_linea_y_la_comparacion_lo_usa()
        {
            var (socio, cuenta) = await _e.NuevoSocioIndependienteAsync();
            var rutina = await GenerarAsync(cuenta, socio.Id);
            var rutinaId = rutina.GetProperty("id").GetGuid();
            var lineas = rutina.GetProperty("ejercicios").EnumerateArray().ToList();
            var l1 = (Linea: lineas[0].GetProperty("id").GetGuid(), Ejercicio: lineas[0].GetProperty("ejercicioId").GetGuid());
            var l2 = (Linea: lineas[1].GetProperty("id").GetGuid(), Ejercicio: lineas[1].GetProperty("ejercicioId").GetGuid());
            var serie = new { ordenSerie = 1, pesoLevantadoKg = 100m, repeticiones = 10, rir = 2, esCalentamiento = false };

            // Un id de línea de otro ejercicio se rechaza (400) y no se guarda nada.
            var invalida = await Como(cuenta).PostAsJsonAsync("/api/Sesiones", new
            {
                socioId = socio.Id, prescripcionEntrenamientoId = rutinaId, inicio = DateTime.UtcNow.AddHours(-1),
                ejercicios = new[] { new { ejercicioId = l1.Ejercicio, orden = 1, prescripcionEjercicioId = (Guid?)l2.Linea, seriesRealizadas = new[] { serie } } },
            });
            Assert.Equal(HttpStatusCode.BadRequest, invalida.StatusCode);

            var r = await Json(await Como(cuenta).PostAsJsonAsync("/api/Sesiones", new
            {
                socioId = socio.Id, prescripcionEntrenamientoId = rutinaId, inicio = DateTime.UtcNow.AddHours(-1),
                ejercicios = new[]
                {
                    new { ejercicioId = l1.Ejercicio, orden = 1, prescripcionEjercicioId = (Guid?)null, seriesRealizadas = new[] { serie } },
                    new { ejercicioId = l2.Ejercicio, orden = 9, prescripcionEjercicioId = (Guid?)l2.Linea, seriesRealizadas = new[] { serie } },
                },
            }), HttpStatusCode.Created);
            var sesionId = r.GetProperty("id").GetGuid();

            var vinculos = new Dictionary<Guid, Guid?>();
            await _e.Api.ConDbAsync(async db => vinculos = await db.SesionesEjercicios.Where(x => x.SesionId == sesionId)
                .ToDictionaryAsync(x => x.EjercicioId, x => x.PrescripcionEjercicioId));
            Assert.Equal(l1.Linea, vinculos[l1.Ejercicio]);    // resuelto por ejercicio
            Assert.Equal(l2.Linea, vinculos[l2.Ejercicio]);    // explícito, aunque el orden no coincida

            var comparacion = await Json(await Como(cuenta).GetAsync($"/api/Sesiones/{sesionId}/comparacion"), HttpStatusCode.OK);
            var segundo = comparacion.GetProperty("ejercicios").EnumerateArray().Single(e => e.GetProperty("orden").GetInt32() == 9);
            Assert.Equal(l2.Linea, segundo.GetProperty("prescripcionEjercicioId").GetGuid());
            Assert.Equal(JsonValueKind.Object, segundo.GetProperty("prescripcion").ValueKind);   // con el criterio anterior no se emparejaba
            Assert.Equal(6, comparacion.GetProperty("ejercicios").GetArrayLength());   // 2 ejecutadas y vinculadas + 4 líneas sin ejecutar
        }

        [Fact]
        public async Task Una_sesion_anterior_sin_vinculo_se_compara_por_ejercicio_y_orden()
        {
            var (socio, cuenta) = await _e.NuevoSocioIndependienteAsync();
            var rutina = await GenerarAsync(cuenta, socio.Id);
            var linea = rutina.GetProperty("ejercicios")[0];
            var sesionId = Guid.NewGuid();
            await _e.Api.ConDbAsync(db =>
            {
                db.Sesiones.Add(new SesionEntrenamiento
                {
                    Id = sesionId, SocioId = socio.Id, Inicio = DateTime.UtcNow.AddDays(-1),
                    PrescripcionEntrenamientoId = rutina.GetProperty("id").GetGuid(),
                    EjerciciosEjecutados =
                    {
                        new SesionEjercicio
                        {
                            EjercicioId = linea.GetProperty("ejercicioId").GetGuid(), Orden = 1, PrescripcionEjercicioId = null,
                            SeriesRealizadas = { new SerieRealizada { OrdenSerie = 1, PesoLevantadoKg = 80, Repeticiones = 10, RIR = 2 } },
                        },
                    },
                });
                return Task.CompletedTask;
            });

            var comparacion = await Json(await Como(cuenta).GetAsync($"/api/Sesiones/{sesionId}/comparacion"), HttpStatusCode.OK);
            var primero = comparacion.GetProperty("ejercicios").EnumerateArray().First(e => e.GetProperty("orden").GetInt32() == 1);
            Assert.Equal(linea.GetProperty("id").GetGuid(), primero.GetProperty("prescripcionEjercicioId").GetGuid());
            Assert.Equal(1, primero.GetProperty("comparacion").GetProperty("series").GetProperty("realizadasEfectivas").GetInt32());
            Assert.Equal(6, comparacion.GetProperty("ejercicios").GetArrayLength());
        }

        // ---- 6.4: rutina modificada ----

        [Fact]
        public async Task La_rutina_queda_modificada_solo_si_un_ajuste_cambia_la_dosis()
        {
            var socio = await _e.NuevoSocioDeG1Async();

            var sinCambio = await GenerarAsync(_e.Dueno1, socio.Id);
            Assert.Equal("PendienteRevision", sinCambio.GetProperty("estado").GetString());
            var linea = sinCambio.GetProperty("ejercicios")[0];
            await Json(await Como(_e.Dueno1).PostAsJsonAsync($"/api/revisiones/{sinCambio.GetProperty("id").GetGuid()}", new
            {
                decision = "Editada",
                ajustes = new[] { new { prescripcionEjercicioId = linea.GetProperty("id").GetGuid(), seriesObjetivo = linea.GetProperty("seriesObjetivo").GetInt32() } },
            }), HttpStatusCode.OK);

            var conCambio = await GenerarAsync(_e.Dueno1, socio.Id);
            Assert.Equal(2, conCambio.GetProperty("numeroGeneracion").GetInt32());
            var otra = conCambio.GetProperty("ejercicios")[0];
            await Json(await Como(_e.Dueno1).PostAsJsonAsync($"/api/revisiones/{conCambio.GetProperty("id").GetGuid()}", new
            {
                decision = "Editada",
                ajustes = new[] { new { prescripcionEjercicioId = otra.GetProperty("id").GetGuid(), seriesObjetivo = 2 } },
            }), HttpStatusCode.OK);

            var marcas = new Dictionary<Guid, bool>();
            await _e.Api.ConDbAsync(async db => marcas = await db.PrescripcionesEntrenamiento.Where(p => p.SocioId == socio.Id)
                .ToDictionaryAsync(p => p.Id, p => p.EsModificada));
            Assert.False(marcas[sinCambio.GetProperty("id").GetGuid()]);
            Assert.True(marcas[conCambio.GetProperty("id").GetGuid()]);
        }

        [Fact]
        public async Task Un_numero_de_generacion_rechazado_no_se_reutiliza()
        {
            var socio = await _e.NuevoSocioDeG1Async();
            var primera = await GenerarAsync(_e.Dueno1, socio.Id);
            await Json(await Como(_e.Dueno1).PostAsJsonAsync($"/api/revisiones/{primera.GetProperty("id").GetGuid()}",
                new { decision = "Rechazada", comentario = "Motivo sintético" }), HttpStatusCode.OK);
            var segunda = await GenerarAsync(_e.Dueno1, socio.Id);
            Assert.Equal(2, segunda.GetProperty("numeroGeneracion").GetInt32());
            // Mientras haya una pendiente no se genera otra.
            Assert.Equal(HttpStatusCode.BadRequest, (await Como(_e.Dueno1).PostAsync($"/api/Socio/{socio.Id}/prescripciones/piernas", null)).StatusCode);
        }

        // ---- KPIs ----

        [Fact]
        public async Task Los_KPIs_solo_incluyen_socios_sinteticos()
        {
            var (socio, cuenta) = await _e.NuevoSocioIndependienteAsync();
            var rutina = await GenerarAsync(cuenta, socio.Id);
            var linea = rutina.GetProperty("ejercicios")[0];
            foreach (var (dias, kg) in new[] { (14, 100m), (7, 102.5m) })
            {
                await Json(await Como(cuenta).PostAsJsonAsync("/api/Sesiones", new
                {
                    socioId = socio.Id, prescripcionEntrenamientoId = rutina.GetProperty("id").GetGuid(), inicio = DateTime.UtcNow.AddDays(-dias),
                    ejercicios = new[] { new { ejercicioId = linea.GetProperty("ejercicioId").GetGuid(), orden = 1,
                        seriesRealizadas = new[] { new { ordenSerie = 1, pesoLevantadoKg = kg, repeticiones = 10, rir = 2, esCalentamiento = false } } } },
                }), HttpStatusCode.Created);
            }

            var real = await Json(await Como(cuenta).GetAsync($"/api/Socio/{socio.Id}/kpis-progresion"), HttpStatusCode.OK);
            Assert.False(real.GetProperty("incluido").GetBoolean());
            Assert.Equal(0, real.GetProperty("k1DeltaCargaMaxima").GetArrayLength());

            await _e.Api.ConDbAsync(async db => (await db.Socios.SingleAsync(s => s.Id == socio.Id)).OrigenDatos = OrigenDatos.Sintetico);
            var sintetico = await Json(await Como(cuenta).GetAsync($"/api/Socio/{socio.Id}/kpis-progresion"), HttpStatusCode.OK);
            Assert.True(sintetico.GetProperty("incluido").GetBoolean());
            var k1 = sintetico.GetProperty("k1DeltaCargaMaxima").EnumerateArray().ToList();
            Assert.Equal(2, k1.Count);
            Assert.Equal(2.5m, k1[1].GetProperty("deltaKg").GetDecimal());
            var k3 = sintetico.GetProperty("k3Adherencia")[0];
            Assert.Equal(linea.GetProperty("id").GetGuid(), k3.GetProperty("prescripcionEjercicioId").GetGuid());
            Assert.Equal(2, sintetico.GetProperty("k4IndiceSeguridad").GetProperty("sesionesEfectivas").GetInt32());
        }

        /// <summary>API, gimnasios, personal y catálogo del protocolo (datos sintéticos).</summary>
        public class Escenario : IAsyncLifetime
        {
            public GymBroApiFactory Api { get; } = new();
            public Gimnasio G1 { get; } = new() { Nombre = "Piernas 1", Codigo = "PIERNAS-1", RequiereAprobacionRutinas = true };
            public Usuario Dueno1 { get; private set; } = null!;
            public Dictionary<string, Guid> Protocolo { get; } = new();
            public Guid LegsDataset { get; } = Guid.NewGuid();
            private int _n;

            public async Task InitializeAsync()
            {
                await Api.InitializeAsync();
                Dueno1 = new Usuario { Email = "dueno@piernas.test", NombreCompleto = "Dueño Piernas", Rol = RolUsuario.AdministradorGimnasio, GimnasioId = G1.Id, PasswordHash = "-" };

                var filas = new (string Clave, GrupoMuscularPiernas G, RolEjercicioProtocolo R, ClaseEjercicio C, NivelLimitacion? Rod, NivelLimitacion? Lum, short O)[]
                {
                    ("Prensa", GrupoMuscularPiernas.Cuadriceps, RolEjercicioProtocolo.Referencia, ClaseEjercicio.Multiarticular, NivelLimitacion.Alta, NivelLimitacion.Alta, 1),
                    ("Hack", GrupoMuscularPiernas.Cuadriceps, RolEjercicioProtocolo.Referencia, ClaseEjercicio.Multiarticular, NivelLimitacion.Alta, null, 2),
                    ("Trasera", GrupoMuscularPiernas.Cuadriceps, RolEjercicioProtocolo.Variable, ClaseEjercicio.Multiarticular, NivelLimitacion.Alta, NivelLimitacion.Moderada, 1),
                    ("Frontal", GrupoMuscularPiernas.Cuadriceps, RolEjercicioProtocolo.Variable, ClaseEjercicio.Multiarticular, NivelLimitacion.Alta, NivelLimitacion.Alta, 2),
                    ("Extension", GrupoMuscularPiernas.Cuadriceps, RolEjercicioProtocolo.Variable, ClaseEjercicio.Aislamiento, NivelLimitacion.Moderada, null, 3),
                    ("CurlSentado", GrupoMuscularPiernas.Isquiotibiales, RolEjercicioProtocolo.Referencia, ClaseEjercicio.Aislamiento, null, null, 1),
                    ("CurlAcostado", GrupoMuscularPiernas.Isquiotibiales, RolEjercicioProtocolo.Variable, ClaseEjercicio.Aislamiento, null, null, 1),
                    ("PesoMuerto", GrupoMuscularPiernas.Isquiotibiales, RolEjercicioProtocolo.Variable, ClaseEjercicio.Multiarticular, null, NivelLimitacion.Moderada, 2),
                    ("TalonesPie", GrupoMuscularPiernas.Pantorrillas, RolEjercicioProtocolo.Variable, ClaseEjercicio.Aislamiento, null, null, 1),
                    ("TalonesSentado", GrupoMuscularPiernas.Pantorrillas, RolEjercicioProtocolo.Variable, ClaseEjercicio.Aislamiento, null, null, 2),
                };

                await Api.ConDbAsync(db =>
                {
                    db.Gimnasios.Add(G1);
                    db.Usuarios.Add(Dueno1);
                    db.Ejercicios.Add(new Ejercicio { Id = LegsDataset, Nombre = "Leg Press (dataset sintetico)", ZonaObjetivo = "Legs", Origen = OrigenEjercicio.Dataset });
                    foreach (var f in filas)
                    {
                        var id = Guid.NewGuid();
                        Protocolo[f.Clave] = id;
                        db.Ejercicios.Add(new Ejercicio { Id = id, Nombre = $"{f.Clave} (piernas sintetico)", ZonaObjetivo = "Legs", Origen = OrigenEjercicio.Proyecto });
                        db.EjerciciosProtocolo.Add(new EjercicioProtocolo
                        {
                            ProtocoloCodigo = "PIERNAS", ProtocoloVersion = "piernas-1.0", EjercicioId = id, Grupo = f.G, Rol = f.R, Clase = f.C,
                            RodillaExcluirDesde = f.Rod, LumbarExcluirDesde = f.Lum, OrdenRotacion = f.O,
                        });
                    }
                    return Task.CompletedTask;
                });
            }

            private Socio Socio(Guid? gimnasio, bool consentimiento, OrigenDatos origen)
            {
                var n = Interlocked.Increment(ref _n);
                return new Socio
                {
                    Id = Guid.NewGuid(), Nombre = $"Socio{n}", Apellido = "Sintetico", Email = $"socio{n}@piernas.test", Genero = "F",
                    FechaNacimiento = new DateTime(1995, 1, 1, 0, 0, 0, DateTimeKind.Utc), GimnasioId = gimnasio,
                    Objetivo = ObjetivoEntrenamiento.Mantenimiento, Nivel = NivelExperiencia.Intermedio, OrigenDatos = origen,
                    ConsentimientoDatosSaludFecha = consentimiento ? DateTime.UtcNow : null,
                };
            }

            /// <summary>Socio independiente con cuenta y medición (su rutina queda aprobada sin revisión).</summary>
            public async Task<(Socio Socio, Usuario Cuenta)> NuevoSocioIndependienteAsync(OrigenDatos origen = OrigenDatos.Real, bool consentimiento = true)
            {
                var socio = Socio(null, consentimiento, origen);
                var cuenta = new Usuario { Email = socio.Email, NombreCompleto = socio.NombreCompleto, Rol = RolUsuario.Socio, SocioId = socio.Id, PasswordHash = "-" };
                await Api.ConDbAsync(db =>
                {
                    db.Socios.Add(socio);
                    db.Usuarios.Add(cuenta);
                    db.MedicionesFisicas.Add(new MedicionFisica { SocioId = socio.Id, PesoKg = 70, AlturaCm = 170, IMC = 24.2m });
                    return Task.CompletedTask;
                });
                return (socio, cuenta);
            }

            /// <summary>Socio del gimnasio 1 (exige revisión), con consentimiento y medición.</summary>
            public async Task<Socio> NuevoSocioDeG1Async()
            {
                var socio = Socio(G1.Id, true, OrigenDatos.Real);
                await Api.ConDbAsync(db =>
                {
                    db.Socios.Add(socio);
                    db.MedicionesFisicas.Add(new MedicionFisica { SocioId = socio.Id, PesoKg = 70, AlturaCm = 170, IMC = 24.2m });
                    return Task.CompletedTask;
                });
                return socio;
            }

            public async Task DisposeAsync() => await Api.DisposeAsync();
        }
    }
}
