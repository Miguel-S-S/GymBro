using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GymBro.Application.Services;
using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Domain.Rules;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace GymBro.Tests.Integration
{
    /// <summary>
    /// Etapa 10a: reglas deterministas de seguridad contra la API real y PostgreSQL descartable.
    /// Escenario SINTÉTICO de prueba (sin datos reales):
    ///   G1 (revisión obligatoria): Dueño, Personal Trainer asignado al socio A, otro Personal Trainer sin asignar, socio A
    ///   (con cuenta y consentimiento), socio S (sin consentimiento) y socio R (para la revisión).
    ///   G2: Dueño. Socio independiente I (con cuenta). Administrador de la Plataforma.
    ///   Catálogo: un ejercicio por repeticiones (press) y uno en segundos.
    /// </summary>
    public class ReglasSeguridadIntegracionTests : IClassFixture<ReglasSeguridadIntegracionTests.Escenario>
    {
        public sealed record Alerta(Guid Id, Guid SocioId, string ReglaCodigo, string Tipo, string Severidad, string Mensaje, string Estado,
            Guid? SesionEjercicioId, Guid? RegistroBienestarId, string? Ejercicio, decimal? CargaSugeridaKg, JsonElement Detalle);
        public sealed record Violacion(string Codigo, string Mensaje, int? Orden);
        public sealed record RutinaPendiente(Guid Id, Guid SocioId, List<Violacion> AdvertenciasLimites);

        private readonly Escenario _e;
        public ReglasSeguridadIntegracionTests(Escenario e) { _e = e; }

        private HttpClient Como(Usuario u) => _e.Api.ClienteComo(u);

        private static object Serie(int orden, decimal kg, int reps, int? rir, bool calentamiento = false) =>
            new { ordenSerie = orden, pesoLevantadoKg = kg, repeticiones = reps, rir, esCalentamiento = calentamiento, tiempoDescansoSegundos = 90 };

        private object Sesion(Guid socioId, Guid? prescripcionId, params object[] series) => new
        {
            socioId,
            prescripcionEntrenamientoId = prescripcionId,
            inicio = DateTime.UtcNow.AddHours(-1),
            fin = DateTime.UtcNow,
            ejercicios = new[] { new { ejercicioId = _e.Press, orden = 1, seriesRealizadas = series } }
        };

        private async Task<Guid> RegistrarAsync(Usuario quien, Guid socioId, Guid? prescripcionId, params object[] series)
        {
            var r = await Como(quien).PostAsJsonAsync("/api/Sesiones", Sesion(socioId, prescripcionId, series));
            Assert.True(r.StatusCode == HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
            return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        }

        private async Task<List<AlertaSeguridad>> FilasAsync(Guid socioId, bool incluirBajas = false)
        {
            var filas = new List<AlertaSeguridad>();
            await _e.Api.ConDbAsync(async db => filas = await db.AlertasSeguridad.IgnoreQueryFilters()
                .Where(a => a.SocioId == socioId && (incluirBajas || a.FechaBaja == null)).ToListAsync());
            return filas;
        }

        private async Task<List<Alerta>> ListarAsync(Usuario quien, string query = "")
        {
            var r = await Como(quien).GetAsync($"/api/alertas{query}");
            Assert.True(r.IsSuccessStatusCode, $"{(int)r.StatusCode} {await r.Content.ReadAsStringAsync()}");
            return (await r.Content.ReadFromJsonAsync<List<Alerta>>())!;
        }

        /// <summary>Reevalúa como proceso de sistema (equivalente a volver a evaluar la misma sesión o el mismo día).</summary>
        private async Task ReevaluarSesionAsync(Guid sesionId)
        {
            using var scope = _e.Api.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IReglasSeguridadService>().EvaluarSesionAsync(sesionId);
        }

        // ---------------- Sesiones: R-RIR-1 y R-PROG-1 ----------------

        [Fact]
        public async Task Una_sesion_con_esfuerzo_mayor_genera_RIR1_con_entradas_umbrales_y_version_y_sin_sugerencia()
        {
            // RIR objetivo 3; mediana realizada 1 (calentamiento excluido).
            var sesion = await RegistrarAsync(_e.UsuarioSocioA, _e.SocioA.Id, _e.RutinaA,
                Serie(1, 20, 12, 5, calentamiento: true), Serie(2, 40, 10, 1), Serie(3, 40, 10, 1), Serie(4, 40, 9, 2));

            var filas = (await FilasAsync(_e.SocioA.Id)).Where(a => a.SesionId == sesion).ToList();
            var rir = Assert.Single(filas);   // con R-RIR-1 no hay sugerencia de carga
            Assert.Equal((CodigosRegla.RirMayorAlPlanificado, TipoAlerta.Alerta, SeveridadAlerta.Advertencia, EstadoAlerta.Pendiente),
                (rir.ReglaCodigo, rir.Tipo, rir.Severidad, rir.Estado));
            Assert.Equal(_e.G1.Id, rir.GimnasioId);

            using var d = JsonDocument.Parse(rir.Detalle);
            Assert.Equal(ReglasSeguridadConfig.ReglasVersion, d.RootElement.GetProperty("reglas_version").GetString());
            Assert.Equal(3, d.RootElement.GetProperty("entradas").GetProperty("rir_objetivo").GetInt32());
            Assert.Equal(2m, d.RootElement.GetProperty("umbrales").GetProperty("diferencia_minima").GetDecimal());
            Assert.Equal(1m, d.RootElement.GetProperty("resultado").GetProperty("mediana_rir").GetDecimal());
        }

        [Fact]
        public async Task Una_sesion_que_alcanza_el_maximo_sugiere_subir_y_el_socio_la_ve()
        {
            var sesion = await RegistrarAsync(_e.UsuarioSocioA, _e.SocioA.Id, _e.RutinaA,
                Serie(1, 41, 12, 3), Serie(2, 41, 12, 3), Serie(3, 41, 12, 4));

            var sugerencia = Assert.Single((await FilasAsync(_e.SocioA.Id)).Where(a => a.SesionId == sesion));
            Assert.Equal((CodigosRegla.Progresion, TipoAlerta.Sugerencia, SeveridadAlerta.Informativa), (sugerencia.ReglaCodigo, sugerencia.Tipo, sugerencia.Severidad));

            var delSocio = await ListarAsync(_e.UsuarioSocioA, "?estado=Pendiente");
            var a = Assert.Single(delSocio, x => x.SesionEjercicioId == sugerencia.SesionEjercicioId);
            Assert.Equal((42m, "Press de prueba"), (a.CargaSugeridaKg, a.Ejercicio));
            Assert.Contains("próxima vez, 42 kg", a.Mensaje);
        }

        [Fact]
        public async Task Reevaluar_no_duplica_corregir_el_dato_retira_y_una_alerta_vista_sigue_vista()
        {
            var sesion = await RegistrarAsync(_e.UsuarioSocioA, _e.SocioA.Id, _e.RutinaA, Serie(1, 40, 10, 0), Serie(2, 40, 10, 1));
            var alerta = Assert.Single((await FilasAsync(_e.SocioA.Id)).Where(a => a.SesionId == sesion));

            // Vista por el Personal Trainer asignado; reevaluar con el mismo resultado no la vuelve a pendiente ni duplica.
            Assert.Equal(HttpStatusCode.OK, (await Como(_e.Trainer).PutAsync($"/api/alertas/{alerta.Id}/vista", null)).StatusCode);
            await ReevaluarSesionAsync(sesion);
            await ReevaluarSesionAsync(sesion);
            var tras = Assert.Single((await FilasAsync(_e.SocioA.Id, incluirBajas: true)).Where(a => a.SesionId == sesion));
            Assert.Equal((alerta.Id, EstadoAlerta.Vista, _e.Trainer.Id), (tras.Id, tras.Estado, tras.VistaPorUsuarioId));

            // Se corrige el RIR registrado: la condición deja de cumplirse y la alerta se retira (baja lógica).
            await _e.Api.ConDbAsync(async db => await db.SeriesRealizadas.IgnoreQueryFilters()
                .Where(s => s.SesionEjercicio.SesionId == sesion).ExecuteUpdateAsync(x => x.SetProperty(s => s.RIR, 3)));
            await ReevaluarSesionAsync(sesion);
            var retirada = Assert.Single((await FilasAsync(_e.SocioA.Id, incluirBajas: true)).Where(a => a.SesionId == sesion));
            Assert.NotNull(retirada.FechaBaja);
            Assert.DoesNotContain(await ListarAsync(_e.UsuarioSocioA), x => x.Id == alerta.Id);
        }

        [Fact]
        public async Task Sin_consentimiento_la_sesion_se_guarda_y_no_se_evalua()
        {
            var sesion = await RegistrarAsync(_e.Dueno1, _e.SocioSinConsentimiento.Id, _e.RutinaSinConsentimiento, Serie(1, 40, 10, 0), Serie(2, 40, 10, 0));
            Assert.NotEqual(Guid.Empty, sesion);
            Assert.Empty(await FilasAsync(_e.SocioSinConsentimiento.Id, incluirBajas: true));
        }

        // ---------------- Bienestar: R-FC-1 ----------------

        private static string Hoy() => DiaLocal.Hoy(TimeProvider.System).ToString("yyyy-MM-dd");
        private static string Ayer() => DiaLocal.Hoy(TimeProvider.System).AddDays(-1).ToString("yyyy-MM-dd");

        private Task<HttpResponseMessage> BienestarAsync(Usuario quien, string fecha, short? fc) =>
            Como(quien).PutAsJsonAsync($"/api/bienestar/{fecha}", new { estres = (short)3, frecuenciaCardiacaReposo = fc });

        [Fact]
        public async Task FC_de_reposo_alta_genera_alerta_y_corregirla_la_retira()
        {
            // Línea base: 6 días anteriores a hoy con 60 lpm (sembrados).
            await _e.SembrarFcAsync(_e.SocioFc.Id, Enumerable.Range(2, 6).Select(i => (i, (short?)60)));

            Assert.Equal(HttpStatusCode.OK, (await BienestarAsync(_e.UsuarioSocioFc, Hoy(), 70)).StatusCode);
            var alerta = Assert.Single(await FilasAsync(_e.SocioFc.Id));
            Assert.Equal(CodigosRegla.FcReposo, alerta.ReglaCodigo);
            Assert.Contains("FC de reposo más alta que tu referencia (+16,7 %)", alerta.Mensaje);
            using (var d = JsonDocument.Parse(alerta.Detalle))
            {
                Assert.Equal(60m, d.RootElement.GetProperty("resultado").GetProperty("linea_base_lpm").GetDecimal());
                Assert.Equal(16.7m, d.RootElement.GetProperty("resultado").GetProperty("variacion_porcentaje").GetDecimal());
                Assert.Equal(14, d.RootElement.GetProperty("umbrales").GetProperty("dias_linea_base").GetInt32());
            }

            // Mismo valor otra vez: no duplica. Corregido a 62: se retira. Sin FC (borrado): sigue retirada.
            await BienestarAsync(_e.UsuarioSocioFc, Hoy(), 70);
            Assert.Single(await FilasAsync(_e.SocioFc.Id, incluirBajas: true));
            await BienestarAsync(_e.UsuarioSocioFc, Hoy(), 62);
            Assert.Empty(await FilasAsync(_e.SocioFc.Id));
            await BienestarAsync(_e.UsuarioSocioFc, Hoy(), null);
            Assert.Empty(await FilasAsync(_e.SocioFc.Id));
            Assert.Single(await FilasAsync(_e.SocioFc.Id, incluirBajas: true));
        }

        [Fact]
        public async Task Corregir_ayer_reevalua_los_dias_posteriores_con_registro()
        {
            // 4 días con dato antes de ayer: hoy no tiene línea base suficiente.
            await _e.SembrarFcAsync(_e.SocioVentana.Id, Enumerable.Range(2, 4).Select(i => (i, (short?)60)));
            await BienestarAsync(_e.UsuarioSocioVentana, Hoy(), 70);
            Assert.Empty(await FilasAsync(_e.SocioVentana.Id));

            // Al cargar ayer, hoy pasa a tener 5 registros previos: se reevalúa y aparece la alerta de hoy.
            await BienestarAsync(_e.UsuarioSocioVentana, Ayer(), 60);
            var alerta = Assert.Single(await FilasAsync(_e.SocioVentana.Id));
            using var d = JsonDocument.Parse(alerta.Detalle);
            Assert.Equal(Hoy(), d.RootElement.GetProperty("entradas").GetProperty("fecha").GetString());
        }

        // ---------------- Aislamiento ----------------

        [Fact]
        public async Task Aislamiento_de_las_alertas_por_rol()
        {
            await RegistrarAsync(_e.UsuarioSocioA, _e.SocioA.Id, _e.RutinaA, Serie(1, 40, 10, 0), Serie(2, 40, 10, 0));
            var alerta = (await FilasAsync(_e.SocioA.Id)).First();

            Assert.Contains(await ListarAsync(_e.Dueno1, $"?socioId={_e.SocioA.Id}"), a => a.Id == alerta.Id);
            Assert.Contains(await ListarAsync(_e.Dueno1), a => a.Id == alerta.Id);
            Assert.Contains(await ListarAsync(_e.Trainer), a => a.Id == alerta.Id);
            Assert.Contains(await ListarAsync(_e.UsuarioSocioA), a => a.Id == alerta.Id);      // socio vinculado: ve las suyas
            Assert.DoesNotContain(await ListarAsync(_e.TrainerNoAsignado), a => a.Id == alerta.Id);
            Assert.DoesNotContain(await ListarAsync(_e.Dueno2), a => a.Id == alerta.Id);

            Assert.Equal(HttpStatusCode.NotFound, (await Como(_e.Dueno2).GetAsync($"/api/alertas?socioId={_e.SocioA.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Como(_e.TrainerNoAsignado).GetAsync($"/api/alertas?socioId={_e.SocioA.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Como(_e.UsuarioSocioIndependiente).GetAsync($"/api/alertas?socioId={_e.SocioA.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Como(_e.SuperAdmin).GetAsync($"/api/alertas?socioId={_e.SocioA.Id}")).StatusCode);

            Assert.Equal(HttpStatusCode.NotFound, (await Como(_e.Dueno2).PutAsync($"/api/alertas/{alerta.Id}/vista", null)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Como(_e.TrainerNoAsignado).PutAsync($"/api/alertas/{alerta.Id}/vista", null)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Como(_e.UsuarioSocioIndependiente).PutAsync($"/api/alertas/{alerta.Id}/vista", null)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Como(_e.SuperAdmin).PutAsync($"/api/alertas/{alerta.Id}/vista", null)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Como(_e.Dueno1).GetAsync("/api/alertas?estado=2")).StatusCode);
        }

        [Fact]
        public async Task Marcar_vista_queda_auditado_sin_valores_de_salud()
        {
            await RegistrarAsync(_e.UsuarioSocioA, _e.SocioA.Id, _e.RutinaA, Serie(1, 40, 10, 0), Serie(2, 40, 10, 0), Serie(3, 40, 10, 0));
            var alerta = (await FilasAsync(_e.SocioA.Id)).First(a => a.Estado == EstadoAlerta.Pendiente);

            var r = await Como(_e.UsuarioSocioA).PutAsync($"/api/alertas/{alerta.Id}/vista", null);
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);

            AuditLog? log = null;
            await _e.Api.ConDbAsync(async db => log = await db.AuditLogs.SingleAsync(l => l.Accion == "ALERTA_VISTA" && l.EntidadId == alerta.Id.ToString()));
            Assert.Equal(alerta.ReglaCodigo, log!.Detalle);
        }

        // ---------------- Tolerancia a fallos (D6) ----------------

        [Fact]
        public async Task Un_fallo_de_las_reglas_no_impide_registrar_la_sesion_ni_guardar_el_bienestar()
        {
            var api = new ApiConReglasQueFallan();
            await api.InitializeAsync();
            try
            {
            var g = new Gimnasio { Nombre = "Falla", Codigo = "FA-1" };
            var socio = Escenario.NuevoSocio("falla@reglas.test", g.Id);
            var cuenta = new Usuario { Email = socio.Email, NombreCompleto = "Socio", Rol = RolUsuario.Socio, SocioId = socio.Id, PasswordHash = "-" };
            var ejercicio = new Ejercicio { Id = Guid.NewGuid(), Nombre = "Press falla", ZonaObjetivo = "Chest", MusculoPrincipal = "-", Ejecucion = "-", Dificultad = 1 };
            await api.ConDbAsync(db => { db.Gimnasios.Add(g); db.Socios.Add(socio); db.Usuarios.Add(cuenta); db.Ejercicios.Add(ejercicio); return Task.CompletedTask; });

            var sesion = await api.ClienteComo(cuenta).PostAsJsonAsync("/api/Sesiones", new
            {
                socioId = socio.Id, inicio = DateTime.UtcNow.AddHours(-1), fin = DateTime.UtcNow,
                ejercicios = new[] { new { ejercicioId = ejercicio.Id, orden = 1, seriesRealizadas = new[] { Serie(1, 40, 10, 2) } } }
            });
            Assert.Equal(HttpStatusCode.Created, sesion.StatusCode);
            var bienestar = await api.ClienteComo(cuenta).PutAsJsonAsync($"/api/bienestar/{Hoy()}", new { frecuenciaCardiacaReposo = (short)60 });
            Assert.Equal(HttpStatusCode.OK, bienestar.StatusCode);

            int sesiones = 0, registros = 0;
            await api.ConDbAsync(async db =>
            {
                sesiones = await db.Sesiones.IgnoreQueryFilters().CountAsync(s => s.SocioId == socio.Id);
                registros = await db.RegistrosBienestarDiario.IgnoreQueryFilters().CountAsync(r => r.SocioId == socio.Id);
            });
            Assert.Equal((1, 1), (sesiones, registros));
            Assert.Equal(2, api.Reglas.Llamadas);
            }
            finally { await api.DisposeAsync(); }
        }

        // ---------------- Revisión: R-LIM ----------------

        [Fact]
        public async Task La_revision_muestra_las_violaciones_las_registra_y_no_bloquea()
        {
            var pendientes = await (await Como(_e.Dueno1).GetAsync("/api/revisiones/pendientes")).Content.ReadFromJsonAsync<List<RutinaPendiente>>();
            var rutina = Assert.Single(pendientes!, p => p.Id == _e.RutinaFueraDeLimites);
            Assert.Contains(rutina.AdvertenciasLimites, v => v.Codigo == ValidadorPrescripcion.Series && v.Orden == 1);
            Assert.Contains(rutina.AdvertenciasLimites, v => v.Codigo == ValidadorPrescripcion.RirPrincipiante);

            var r = await Como(_e.Dueno1).PostAsJsonAsync($"/api/revisiones/{_e.RutinaFueraDeLimites}", new { decision = "Aprobada" });
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);

            string? registradas = null;
            await _e.Api.ConDbAsync(async db => registradas = (await db.RevisionesPrescripcion.SingleAsync(x => x.PrescripcionEntrenamientoId == _e.RutinaFueraDeLimites)).ViolacionesLimites);
            Assert.NotNull(registradas);
            Assert.Contains(ValidadorPrescripcion.Series, registradas);

            // Un valor imposible sigue rechazándose (400), como antes de la etapa.
            Assert.Equal(HttpStatusCode.BadRequest, (await Como(_e.Dueno1).PostAsJsonAsync($"/api/revisiones/{_e.RutinaImposible}", new
            {
                decision = "Editada",
                ajustes = new[] { new { prescripcionEjercicioId = _e.LineaImposible, seriesObjetivo = 0 } }
            })).StatusCode);
        }

        [Fact]
        public async Task El_baseline_se_genera_para_todas_las_combinaciones_de_nivel_y_objetivo()
        {
            foreach (var nivel in Enum.GetValues<NivelExperiencia>())
                foreach (var objetivo in Enum.GetValues<ObjetivoEntrenamiento>())
                {
                    await _e.Api.ConDbAsync(async db =>
                    {
                        var s = await db.Socios.SingleAsync(x => x.Id == _e.SocioIndependiente.Id);
                        s.Nivel = nivel;
                        s.Objetivo = objetivo;
                    });
                    var r = await Como(_e.UsuarioSocioIndependiente).PostAsync($"/api/Socio/{_e.SocioIndependiente.Id}/prescripciones/baseline", null);
                    Assert.True(r.StatusCode == HttpStatusCode.Created, $"{nivel}/{objetivo}: {(int)r.StatusCode} {await r.Content.ReadAsStringAsync()}");
                }
        }

        [Fact]
        public async Task Un_baseline_fuera_de_limites_por_configuracion_responde_catalogo_incompleto_con_mensaje_claro()
        {
            var api = new ApiConBaselineFueraDeLimites();
            await api.InitializeAsync();
            try
            {
                var socio = Escenario.NuevoSocio("limite@reglas.test", null);   // principiante
                var cuenta = new Usuario { Email = socio.Email, NombreCompleto = "Socio", Rol = RolUsuario.Socio, SocioId = socio.Id, PasswordHash = "-" };
                await api.ConDbAsync(db =>
                {
                    db.Socios.Add(socio);
                    db.Usuarios.Add(cuenta);
                    db.MedicionesFisicas.Add(new MedicionFisica { SocioId = socio.Id, PesoKg = 70, AlturaCm = 175, IMC = 22.9m });
                    return Task.CompletedTask;
                });

                var r = await api.ClienteComo(cuenta).PostAsync($"/api/Socio/{socio.Id}/prescripciones/baseline", null);

                Assert.Equal(HttpStatusCode.InternalServerError, r.StatusCode);
                var problema = await r.Content.ReadFromJsonAsync<JsonElement>();
                Assert.Equal("Catálogo incompleto", problema.GetProperty("title").GetString());
                Assert.Contains(ValidadorPrescripcion.RirPrincipiante, problema.GetProperty("detail").GetString());
                var rutinas = -1;
                await api.ConDbAsync(async db => rutinas = await db.PrescripcionesEntrenamiento.IgnoreQueryFilters().CountAsync(p => p.SocioId == socio.Id));
                Assert.Equal(0, rutinas);
            }
            finally { await api.DisposeAsync(); }
        }

        /// <summary>RIR por defecto del baseline en 1: viola el mínimo de 2 para principiantes.</summary>
        public sealed class ApiConBaselineFueraDeLimites : GymBroApiFactory
        {
            protected override void ConfigurarExtra(IWebHostBuilder builder) => builder.UseSetting("Baseline:RirPorDefecto", "1");
        }

        // ---------------- Escenario ----------------

        /// <summary>Servicio de reglas que siempre falla, para probar la tolerancia (D6).</summary>
        public sealed class ReglasQueFallan : IReglasSeguridadService
        {
            public int Llamadas;
            public Task EvaluarSesionAsync(Guid sesionId) { Interlocked.Increment(ref Llamadas); throw new InvalidOperationException("Fallo simulado."); }
            public Task EvaluarBienestarAsync(Guid registroId) { Interlocked.Increment(ref Llamadas); throw new InvalidOperationException("Fallo simulado."); }
            public IReadOnlyList<ViolacionLimite> ValidarPrescripcion(IReadOnlyCollection<LineaPrescripcion> l, NivelExperiencia? n) => Array.Empty<ViolacionLimite>();
        }

        public sealed class ApiConReglasQueFallan : GymBroApiFactory
        {
            public ReglasQueFallan Reglas { get; } = new();
            protected override void ConfigurarExtra(IWebHostBuilder builder) =>
                builder.ConfigureTestServices(s => { s.RemoveAll<IReglasSeguridadService>(); s.AddSingleton<IReglasSeguridadService>(Reglas); });
        }

        public class Escenario : IAsyncLifetime
        {
            public GymBroApiFactory Api { get; } = new();
            public Gimnasio G1 { get; } = new() { Nombre = "Reglas 1", Codigo = "RS-1", RequiereAprobacionRutinas = true };
            public Gimnasio G2 { get; } = new() { Nombre = "Reglas 2", Codigo = "RS-2" };
            public Usuario Dueno1 { get; private set; } = null!;
            public Usuario Dueno2 { get; private set; } = null!;
            public Usuario Trainer { get; private set; } = null!;
            public Usuario TrainerNoAsignado { get; private set; } = null!;
            public Usuario SuperAdmin { get; } = new() { Email = "plataforma@reglas.test", NombreCompleto = "Plataforma", Rol = RolUsuario.SuperAdministrador, PasswordHash = "-" };
            public Socio SocioA { get; private set; } = null!;
            public Usuario UsuarioSocioA { get; private set; } = null!;
            public Socio SocioFc { get; private set; } = null!;
            public Usuario UsuarioSocioFc { get; private set; } = null!;
            public Socio SocioVentana { get; private set; } = null!;
            public Usuario UsuarioSocioVentana { get; private set; } = null!;
            public Socio SocioSinConsentimiento { get; private set; } = null!;
            public Socio SocioRevision { get; private set; } = null!;
            public Socio SocioIndependiente { get; private set; } = null!;
            public Usuario UsuarioSocioIndependiente { get; private set; } = null!;
            public Guid Press { get; } = Guid.NewGuid();
            public Guid Plancha { get; } = Guid.NewGuid();
            public Guid RutinaA { get; private set; }
            public Guid RutinaSinConsentimiento { get; private set; }
            public Guid RutinaFueraDeLimites { get; private set; }
            public Guid RutinaImposible { get; private set; }
            public Guid LineaImposible { get; private set; }

            public async Task InitializeAsync()
            {
                await Api.InitializeAsync();
                Dueno1 = Personal("dueno1@reglas.test", RolUsuario.AdministradorGimnasio, G1.Id);
                Dueno2 = Personal("dueno2@reglas.test", RolUsuario.AdministradorGimnasio, G2.Id);
                Trainer = Personal("trainer@reglas.test", RolUsuario.Entrenador, G1.Id);
                TrainerNoAsignado = Personal("trainer2@reglas.test", RolUsuario.Entrenador, G1.Id);
                (SocioA, UsuarioSocioA) = ConCuenta(NuevoSocio("a@reglas.test", G1.Id));
                (SocioFc, UsuarioSocioFc) = ConCuenta(NuevoSocio("fc@reglas.test", G1.Id));
                (SocioVentana, UsuarioSocioVentana) = ConCuenta(NuevoSocio("ventana@reglas.test", null));
                SocioSinConsentimiento = NuevoSocio("sin@reglas.test", G1.Id);
                SocioSinConsentimiento.ConsentimientoDatosSaludFecha = null;
                SocioRevision = NuevoSocio("revision@reglas.test", G1.Id);
                (SocioIndependiente, UsuarioSocioIndependiente) = ConCuenta(NuevoSocio("indep@reglas.test", null));

                var rutinaA = Rutina(SocioA.Id, EstadoPrescripcion.Aprobada, (Press, 3, 8, 12, 3, 90));
                var rutinaSin = Rutina(SocioSinConsentimiento.Id, EstadoPrescripcion.Aprobada, (Press, 3, 8, 12, 3, 90));
                // Pendiente con 8 series (LIM-SERIES) y RIR 1 en un principiante (LIM-RIR-PRINCIPIANTE).
                var fuera = Rutina(SocioRevision.Id, EstadoPrescripcion.PendienteRevision, (Press, 8, 8, 12, 1, 90));
                var socioImposible = NuevoSocio("imposible@reglas.test", G1.Id);
                var imposible = Rutina(socioImposible.Id, EstadoPrescripcion.PendienteRevision, (Press, 3, 8, 12, 2, 90));
                RutinaA = rutinaA.Id; RutinaSinConsentimiento = rutinaSin.Id; RutinaFueraDeLimites = fuera.Id;
                RutinaImposible = imposible.Id; LineaImposible = imposible.EjerciciosPrescritos.Single().Id;

                await Api.ConDbAsync(db =>
                {
                    db.Gimnasios.AddRange(G1, G2);
                    db.Socios.AddRange(SocioA, SocioFc, SocioVentana, SocioSinConsentimiento, SocioRevision, SocioIndependiente, socioImposible);
                    db.Usuarios.AddRange(Dueno1, Dueno2, Trainer, TrainerNoAsignado, SuperAdmin, UsuarioSocioA, UsuarioSocioFc, UsuarioSocioVentana, UsuarioSocioIndependiente);
                    db.Asignaciones.Add(new AsignacionEntrenadorSocio { GimnasioId = G1.Id, EntrenadorUsuarioId = Trainer.Id, SocioId = SocioA.Id });
                    db.Ejercicios.AddRange(
                        new Ejercicio { Id = Press, Nombre = "Press de prueba", Equipamiento = "Barbell", ZonaObjetivo = "Chest", MusculoPrincipal = "Pectoral", Ejecucion = "-", Dificultad = 1 },
                        new Ejercicio { Id = Plancha, Nombre = "Isometrico de prueba", Equipamiento = "Body Weight", ZonaObjetivo = "Back", MusculoPrincipal = "-", Ejecucion = "-", Dificultad = 1, TipoMedida = TipoMedida.Segundos });
                    db.PrescripcionesEntrenamiento.AddRange(rutinaA, rutinaSin, fuera, imposible);
                    db.MedicionesFisicas.Add(new MedicionFisica { SocioId = SocioIndependiente.Id, PesoKg = 70, AlturaCm = 175, IMC = 22.9m });
                    return Task.CompletedTask;
                });
            }

            public async Task SembrarFcAsync(Guid socioId, IEnumerable<(int DiasAtras, short? Fc)> dias)
            {
                var hoy = DiaLocal.Hoy(TimeProvider.System);
                await Api.ConDbAsync(db =>
                {
                    foreach (var (atras, fc) in dias)
                        db.RegistrosBienestarDiario.Add(new RegistroBienestarDiario { SocioId = socioId, Fecha = hoy.AddDays(-atras), FrecuenciaCardiacaReposo = fc, Estres = 3 });
                    return Task.CompletedTask;
                });
            }

            private static PrescripcionEntrenamiento Rutina(Guid socioId, EstadoPrescripcion estado,
                params (Guid Ejercicio, int Series, int Min, int Max, int Rir, int Descanso)[] lineas)
            {
                var p = new PrescripcionEntrenamiento { SocioId = socioId, OrigenGeneracion = "PRUEBA", Estado = estado };
                var orden = 0;
                foreach (var l in lineas)
                    p.EjerciciosPrescritos.Add(new PrescripcionEjercicio
                    {
                        PrescripcionEntrenamientoId = p.Id, EjercicioId = l.Ejercicio, Orden = ++orden, SeriesObjetivo = l.Series,
                        RepeticionesMinimas = l.Min, RepeticionesMaximas = l.Max, RIRObjetivo = l.Rir, DescansoObjetivoSegundos = l.Descanso,
                    });
                return p;
            }

            public async Task DisposeAsync() => await Api.DisposeAsync();

            private static Usuario Personal(string email, RolUsuario rol, Guid gimnasioId) =>
                new() { Email = email, NombreCompleto = email, Rol = rol, GimnasioId = gimnasioId, PasswordHash = "-" };

            private static (Socio, Usuario) ConCuenta(Socio s) =>
                (s, new Usuario { Email = s.Email, NombreCompleto = s.Email, Rol = RolUsuario.Socio, SocioId = s.Id, PasswordHash = "-" });

            public static Socio NuevoSocio(string email, Guid? gimnasioId) => new()
            {
                Id = Guid.NewGuid(), Nombre = "Socio", Apellido = "Reglas", Email = email, Genero = "F",
                FechaNacimiento = new DateTime(1995, 1, 1, 0, 0, 0, DateTimeKind.Utc), GimnasioId = gimnasioId,
                Objetivo = ObjetivoEntrenamiento.Mantenimiento, Nivel = NivelExperiencia.Principiante,
                ConsentimientoDatosSaludFecha = DateTime.UtcNow,
            };
        }
    }
}
