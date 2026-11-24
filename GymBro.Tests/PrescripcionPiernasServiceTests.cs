using System.Text.Json;
using GymBro.Application.DTOs.Acceso;
using GymBro.Application.Services;
using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Domain.Exceptions;
using GymBro.Domain.Repositories;
using GymBro.Domain.Rules;
using Xunit;

namespace GymBro.Tests
{
    /// <summary>
    /// Etapa 7d: PrescripcionPiernasService con dobles (sin base de datos). Datos SINTÉTICOS de prueba: los textos
    /// "Secreto" representan datos personales que no deben aparecer en el snapshot ni en las observaciones.
    /// </summary>
    public class PrescripcionPiernasServiceTests
    {
        private static readonly DateTimeOffset Ahora = new(2026, 11, 24, 15, 0, 0, TimeSpan.Zero);

        private static Socio NuevoSocio(OrigenDatos origen = OrigenDatos.Sintetico, bool consentimiento = true) => new()
        {
            Id = Guid.NewGuid(), Nombre = "NombreSecreto", Apellido = "ApellidoSecreto", Email = "secreto@correo.test",
            Dni = "99888777", Genero = "F", FechaNacimiento = new DateTime(1990, 5, 10, 0, 0, 0, DateTimeKind.Utc),
            Nivel = NivelExperiencia.Intermedio, Objetivo = ObjetivoEntrenamiento.Mantenimiento, OrigenDatos = origen,
            ConsentimientoDatosSaludFecha = consentimiento ? Ahora.UtcDateTime.AddDays(-30) : null,
        };

        private static List<FilaProtocolo> Filas() => PlanificadorPiernasTests.Catalogo().Select(c => new FilaProtocolo(
            c.EjercicioId, c.Nombre, c.TipoMedida, null, c.Grupo, c.Rol, c.Clase, c.RodillaExcluirDesde, c.LumbarExcluirDesde,
            c.OrdenRotacion)).ToList();

        private sealed class Contexto
        {
            public RepoDoble Repo { get; } = new();
            public AuditDoble Audit { get; } = new();
            public RegistroEventosDoble Eventos { get; } = new();
            public IValidadorPrescripcionService Validador { get; set; } = new ValidadorPrescripcionService(new ReglasSeguridadConfig());

            public PrescripcionPiernasService Servicio() => new(Repo, new AccesoDoble(), new RevisionDoble(), Audit, Validador, Eventos,
                new ProtocoloPiernasConfig(), new ReglasSeguridadConfig(), new RelojFijo(Ahora));
        }

        private static Contexto ConSocio(Socio socio)
        {
            var c = new Contexto();
            c.Repo.Socio = socio;
            c.Repo.Medicion = new MedicionFisica
            {
                Id = Guid.NewGuid(), SocioId = socio.Id, PesoKg = 70, AlturaCm = 170, FrecuenciaCardiacaReposo = 62,
                CondicionMedica = "Hipertension secreta",
            };
            c.Repo.Filas = Filas();
            return c;
        }

        [Fact]
        public async Task Sin_consentimiento_no_genera()
        {
            var c = ConSocio(NuevoSocio(consentimiento: false));
            var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => c.Servicio().GenerarAsync(c.Repo.Socio!.Id));
            Assert.Contains("consentimiento", ex.Message);
            Assert.Empty(c.Repo.Agregadas);
        }

        [Fact]
        public async Task Sin_medicion_no_genera()
        {
            var c = ConSocio(NuevoSocio());
            c.Repo.Medicion = null;
            var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => c.Servicio().GenerarAsync(c.Repo.Socio!.Id));
            Assert.Contains("medición", ex.Message);
        }

        [Fact]
        public async Task Con_una_rutina_pendiente_de_revision_no_genera()
        {
            var c = ConSocio(NuevoSocio());
            c.Repo.Pendiente = true;
            var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => c.Servicio().GenerarAsync(c.Repo.Socio!.Id));
            Assert.Contains("pendiente de revisión", ex.Message);
        }

        [Fact]
        public async Task Socio_inexistente_404()
        {
            var c = new Contexto();
            await Assert.ThrowsAsync<RecursoNoEncontradoException>(() => c.Servicio().GenerarAsync(Guid.NewGuid()));
        }

        [Fact]
        public async Task El_numero_de_generacion_es_el_maximo_mas_uno_aunque_la_ultima_haya_sido_rechazada()
        {
            var c = ConSocio(NuevoSocio());
            c.Repo.MaxNumero = 3;   // incluye una rutina rechazada: el número no se reutiliza
            var dto = await c.Servicio().GenerarAsync(c.Repo.Socio!.Id);
            var p = Assert.Single(c.Repo.Agregadas);
            Assert.Equal(4, p.NumeroGeneracion);
            Assert.Equal(4, dto.NumeroGeneracion);
            // Generación 4: la variable de cuádriceps rota a la primera (índice (4 - 1) mod 3 = 0).
            Assert.Equal(PlanificadorPiernasTests.Trasera, p.EjerciciosPrescritos.Single(l => l.Grupo == GrupoMuscularPiernas.Cuadriceps && l.Rol == RolEjercicioProtocolo.Variable).EjercicioId);
        }

        [Theory]
        [InlineData(OrigenDatos.Sintetico)]
        [InlineData(OrigenDatos.ValidacionFuncional)]
        [InlineData(OrigenDatos.Real)]
        public async Task Copia_el_origen_de_datos_del_socio_y_guarda_la_trazabilidad(OrigenDatos origen)
        {
            var c = ConSocio(NuevoSocio(origen));
            await c.Servicio().GenerarAsync(c.Repo.Socio!.Id);
            var p = Assert.Single(c.Repo.Agregadas);
            Assert.Equal(origen, p.OrigenDatos);
            Assert.Equal(("PIERNAS", "piernas-1.0", ProtocoloPiernasConfig.OrigenGeneracion), (p.ProtocoloCodigo, p.ProtocoloVersion, p.OrigenGeneracion));
            Assert.Equal(1, p.NumeroGeneracion);
            Assert.False(p.ExcluidaEstudio);
            Assert.False(p.EsModificada);
            Assert.Equal(6, p.EjerciciosPrescritos.Count);
            Assert.All(p.EjerciciosPrescritos, l =>
            {
                Assert.NotNull(l.Rol);
                Assert.NotNull(l.Grupo);
                Assert.Equal((short)3, l.TempoExcentricoSeg);
                Assert.NotNull(l.MotivoSeleccion);
                Assert.True(l.RIRObjetivo >= 1);
            });
            using var motivo = JsonDocument.Parse(p.EjerciciosPrescritos.First().MotivoSeleccion!);
            Assert.Equal("piernas-1.0", motivo.RootElement.GetProperty("protocoloVersion").GetString());
            Assert.False(motivo.RootElement.GetProperty("reduccionPorAlerta").GetBoolean());
        }

        [Fact]
        public async Task El_snapshot_y_las_observaciones_no_tienen_datos_personales()
        {
            var c = ConSocio(NuevoSocio());
            c.Repo.Alertas = new List<Guid> { Guid.NewGuid() };
            await c.Servicio().GenerarAsync(c.Repo.Socio!.Id);
            var p = Assert.Single(c.Repo.Agregadas);

            foreach (var texto in new[] { p.EntradaSnapshot!, p.Observaciones! })
            {
                Assert.DoesNotContain("Secreto", texto, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("secreto@correo.test", texto);
                Assert.DoesNotContain("99888777", texto);
                Assert.DoesNotContain("Hipertension", texto, StringComparison.OrdinalIgnoreCase);
            }
            Assert.True(p.Observaciones!.Length <= 500);

            using var s = JsonDocument.Parse(p.EntradaSnapshot!);
            var r = s.RootElement;
            Assert.Equal("piernas-1.0", r.GetProperty("version").GetString());
            Assert.Equal(1, r.GetProperty("numeroGeneracion").GetInt32());
            Assert.Equal(36, r.GetProperty("edad").GetInt32());
            Assert.Equal("Intermedio", r.GetProperty("nivel").GetString());
            Assert.Equal("SinLimitacion", r.GetProperty("limitacionRodilla").GetString());
            Assert.Equal(c.Repo.Medicion!.Id, r.GetProperty("medicionId").GetGuid());
            Assert.Equal(62, r.GetProperty("fcReposo").GetInt32());
            Assert.Equal(1, r.GetProperty("alertasVigentes").GetArrayLength());
            Assert.Equal(64, r.GetProperty("configuracionSha256").GetString()!.Length);
            Assert.False(r.TryGetProperty("nombre", out _));
            // Con alerta vigente, las líneas quedan marcadas con la reducción.
            Assert.All(p.EjerciciosPrescritos, l => Assert.Contains("\"reduccionPorAlerta\":true", l.MotivoSeleccion));
            Assert.Contains("alerta de seguridad vigente", p.Observaciones);
        }

        [Fact]
        public async Task Una_violacion_de_R_LIM_no_persiste_ni_audita()
        {
            var c = ConSocio(NuevoSocio());
            c.Validador = new ValidadorConViolacion();
            var ex = await Assert.ThrowsAsync<CatalogosIncompletosException>(() => c.Servicio().GenerarAsync(c.Repo.Socio!.Id));
            Assert.Contains("LIM-SERIES", ex.Message);
            Assert.Empty(c.Repo.Agregadas);
            Assert.Equal(0, c.Repo.Guardados);
            Assert.Empty(c.Audit.Acciones);
            Assert.Contains(c.Eventos.Mensajes, m => m.Contains("LIM-SERIES"));
        }

        [Fact]
        public async Task Registra_la_auditoria_GENERACION_PIERNAS_sin_datos_de_salud()
        {
            var c = ConSocio(NuevoSocio());
            var dto = await c.Servicio().GenerarAsync(c.Repo.Socio!.Id);
            var (accion, entidad, id, detalle) = Assert.Single(c.Audit.Acciones);
            Assert.Equal(("GENERACION_PIERNAS", nameof(PrescripcionEntrenamiento), dto.Id.ToString()), (accion, entidad, id));
            Assert.Equal(EstadoPrescripcion.PendienteRevision.ToString(), detalle);
            Assert.Equal(1, c.Repo.Guardados);
        }

        [Fact]
        public async Task La_respuesta_incluye_rol_grupo_tempo_y_carga_sugerida_por_linea()
        {
            var c = ConSocio(NuevoSocio());
            var dto = await c.Servicio().GenerarAsync(c.Repo.Socio!.Id);
            Assert.Equal("piernas-1.0", dto.ProtocoloVersion);
            Assert.Equal(new[] { "Referencia", "Referencia", "Variable", "Referencia", "Variable", "Variable" }, dto.Ejercicios.Select(e => e.Rol));
            Assert.All(dto.Ejercicios, e => Assert.Equal(3, e.TempoExcentricoSeg));
            Assert.All(dto.Ejercicios, e => Assert.Null(e.CargaSugeridaKg));   // sin historial
        }

        [Fact]
        public async Task Dos_generaciones_con_las_mismas_entradas_dan_las_mismas_lineas()
        {
            var socio = NuevoSocio();
            var a = ConSocio(socio);
            var b = ConSocio(socio);
            b.Repo.Medicion = a.Repo.Medicion;
            await a.Servicio().GenerarAsync(socio.Id);
            await b.Servicio().GenerarAsync(socio.Id);
            static string Lineas(PrescripcionEntrenamiento p) => JsonSerializer.Serialize(p.EjerciciosPrescritos.Select(l => new
            {
                l.EjercicioId, l.Orden, l.SeriesObjetivo, l.RepeticionesMinimas, l.RepeticionesMaximas, l.RIRObjetivo,
                l.DescansoObjetivoSegundos, l.TempoExcentricoSeg, l.MotivoSeleccion,
            }));
            Assert.Equal(Lineas(a.Repo.Agregadas[0]), Lineas(b.Repo.Agregadas[0]));
            Assert.Equal(a.Repo.Agregadas[0].EntradaSnapshot, b.Repo.Agregadas[0].EntradaSnapshot);
        }

        [Fact]
        public void La_huella_de_la_configuracion_cambia_si_cambia_un_parametro_y_no_si_la_composicion_es_la_por_defecto()
        {
            var porDefecto = PrescripcionPiernasService.HuellaConfiguracion(new ProtocoloPiernasConfig());
            var explicita = new ProtocoloPiernasConfig();
            explicita.Composicion.AddRange(ProtocoloPiernasConfig.ComposicionPorDefecto);
            Assert.Equal(porDefecto, PrescripcionPiernasService.HuellaConfiguracion(explicita));
            Assert.NotEqual(porDefecto, PrescripcionPiernasService.HuellaConfiguracion(new ProtocoloPiernasConfig { RirObjetivo = 3 }));
        }

        [Fact]
        public void Las_limitaciones_solo_aceptan_los_nombres_del_enum()
        {
            Assert.Equal(NivelLimitacion.Moderada, LimitacionesArticularesService.LeerNivel(" moderada ", "rodilla"));
            Assert.Throws<ReglaNegocioException>(() => LimitacionesArticularesService.LeerNivel("2", "rodilla"));
            Assert.Throws<ReglaNegocioException>(() => LimitacionesArticularesService.LeerNivel("Moderada,Alta", "rodilla"));
            Assert.Throws<ReglaNegocioException>(() => LimitacionesArticularesService.LeerNivel(null, "rodilla"));
        }

        // ---- Dobles ----

        private sealed class RepoDoble : IProtocoloPiernasRepository
        {
            public Socio? Socio { get; set; }
            public MedicionFisica? Medicion { get; set; }
            public bool Pendiente { get; set; }
            public List<FilaProtocolo> Filas { get; set; } = new();
            public List<Guid> Alertas { get; set; } = new();
            public int MaxNumero { get; set; }
            public List<PrescripcionEntrenamiento> Agregadas { get; } = new();
            public int Guardados { get; private set; }

            public Task<Socio?> GetSocioAsync(Guid socioId) => Task.FromResult(Socio?.Id == socioId ? Socio : null);
            public Task<MedicionFisica?> GetUltimaMedicionAsync(Guid socioId) => Task.FromResult(Medicion);
            public Task<bool> TienePendienteDeRevisionAsync(Guid socioId) => Task.FromResult(Pendiente);
            public Task<IReadOnlyList<FilaProtocolo>> GetFilasProtocoloAsync(string protocoloCodigo, string version) =>
                Task.FromResult<IReadOnlyList<FilaProtocolo>>(Filas);
            public Task<IReadOnlySet<Guid>?> GetTiposEquipamientoFactiblesAsync(Guid gimnasioId) => Task.FromResult<IReadOnlySet<Guid>?>(null);
            public Task<IReadOnlyList<HistorialEjercicio>> GetUltimosRegistrosAsync(Guid socioId, IReadOnlyCollection<Guid> ejercicioIds) =>
                Task.FromResult<IReadOnlyList<HistorialEjercicio>>(Array.Empty<HistorialEjercicio>());
            public Task<IReadOnlyList<Guid>> GetAlertasVigentesAsync(Guid socioId, DateTime desdeUtc) => Task.FromResult<IReadOnlyList<Guid>>(Alertas);
            public Task<int> GetMaxNumeroGeneracionAsync(Guid socioId, string protocoloCodigo) => Task.FromResult(MaxNumero);
            public Task AddPrescripcionAsync(PrescripcionEntrenamiento prescripcion) { Agregadas.Add(prescripcion); return Task.CompletedTask; }
            public Task<IReadOnlyList<PrescripcionEntrenamiento>> GetPrescripcionesDeSocioAsync(Guid socioId) =>
                Task.FromResult<IReadOnlyList<PrescripcionEntrenamiento>>(Agregadas);
            public Task SaveChangesAsync() { Guardados++; return Task.CompletedTask; }
        }

        private sealed class AccesoDoble : ISocioAccessService
        {
            public Task AsegurarAccesoAsync(Guid socioId) => Task.CompletedTask;
        }

        private sealed class RevisionDoble : IRevisionPrescripcionService
        {
            public Task AplicarPoliticaAsync(PrescripcionEntrenamiento prescripcion)
            {
                prescripcion.DefinirEstadoInicial(requiereRevisionHumana: true);
                return Task.CompletedTask;
            }
            public Task<IEnumerable<RutinaPendienteDto>> ListarPendientesAsync() => throw new NotSupportedException();
            public Task<RutinaPendienteDto> RevisarAsync(Guid prescripcionId, RevisarRutinaRequest request) => throw new NotSupportedException();
        }

        private sealed class AuditDoble : IAuditService
        {
            public List<(string Accion, string Entidad, string? Id, string? Detalle)> Acciones { get; } = new();
            public Task RegistrarAsync(string accion, string entidad, string? entidadId = null, string? detalle = null)
            {
                Acciones.Add((accion, entidad, entidadId, detalle));
                return Task.CompletedTask;
            }
            public Task RegistrarEnGimnasioAsync(string accion, string entidad, string? entidadId, Guid? gimnasioId, string? detalle = null) => Task.CompletedTask;
            public Task<IEnumerable<AuditLogDto>> ConsultarAsync(int cantidad, Guid? gimnasioId, DateTime? desdeUtc = null,
                DateTime? hastaUtc = null, string? accion = null, string? entidad = null) => throw new NotSupportedException();
        }

        private sealed class ValidadorConViolacion : IValidadorPrescripcionService
        {
            public IReadOnlyList<ViolacionLimite> Validar(IReadOnlyCollection<LineaPrescripcion> lineas, NivelExperiencia? nivel) =>
                new[] { new ViolacionLimite(ValidadorPrescripcion.Series, "Series fuera de rango (simulado).", 1) };
            public IReadOnlyList<ViolacionLimite> Validar(PrescripcionEntrenamiento prescripcion, NivelExperiencia? nivel) =>
                throw new NotSupportedException();
        }

        private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => ahora;
        }
    }
}
