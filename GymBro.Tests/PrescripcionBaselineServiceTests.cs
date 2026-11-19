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
    /// <summary>Pruebas del caso de uso de la rutina baseline (Análisis de Fase 2, sección 11) con repositorios en memoria.</summary>
    public class PrescripcionBaselineServiceTests
    {
        private static readonly Guid SocioId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        // ---- Datos de prueba ----
        private static Socio NuevoSocio(bool consentimiento = true) => new()
        {
            Id = SocioId, Nombre = "Lucas", Apellido = "Perez", Genero = "M",
            FechaNacimiento = DateTime.UtcNow.AddYears(-30),
            Objetivo = ObjetivoEntrenamiento.Mantenimiento, Nivel = NivelExperiencia.Principiante,
            ConsentimientoDatosSaludFecha = consentimiento ? DateTime.UtcNow : null,
        };

        private static MedicionFisica NuevaMedicion(decimal imc = 22m, string condicion = "") => new()
        {
            SocioId = SocioId, IMC = imc, CondicionMedica = condicion,
        };

        private static FakeBaselineRepository RepoCompleto()
        {
            var repo = new FakeBaselineRepository { SocioActual = NuevoSocio(), Medicion = NuevaMedicion() };
            foreach (var z in new[] { "Chest", "Back", "Legs", "Arms", "Core" })
            {
                repo.Ejercicios.Add(new EjercicioCandidato(Guid.NewGuid(), $"Ejercicio {z} B", "Barbell", z, z, 2));
                repo.Ejercicios.Add(new EjercicioCandidato(Guid.NewGuid(), $"Ejercicio {z} A", "Dumbbell", z, z, 1));
                repo.Dosificaciones.Add(new DosificacionVolumetrica
                {
                    Id = Guid.NewGuid(), ZonaObjetivo = z, SeriesMinimas = 3, SeriesMaximas = 4,
                    RepeticionesMinimas = 8, RepeticionesMaximas = 12,
                });
            }
            return repo;
        }

        private static PrescripcionBaselineService Servicio(FakeBaselineRepository repo) =>
            new(repo, new FakeAcceso(), new FakeRevision(), new FakeAudit(), new BaselineConfig());

        // ---- Pruebas ----
        [Fact]
        public async Task GeneracionExitosa_CreaRutinaDeCuatroASeisEjercicios_ConValoresPorDefecto()
        {
            var repo = RepoCompleto();

            var rutina = await Servicio(repo).GenerarAsync(SocioId);

            Assert.InRange(rutina.Ejercicios.Count, 4, 6);
            Assert.Equal("PendienteRevision", rutina.Estado);
            Assert.Equal(PrescripcionBaselineService.Origen, rutina.OrigenGeneracion);
            Assert.All(rutina.Ejercicios, e =>
            {
                Assert.Equal(3, e.SeriesObjetivo);
                Assert.Equal(8, e.RepeticionesMinimas);
                Assert.Equal(12, e.RepeticionesMaximas);
                Assert.Equal(2, e.RIRObjetivo);
                Assert.Equal(90, e.DescansoObjetivoSegundos);
            });
            Assert.Equal(Enumerable.Range(1, rutina.Ejercicios.Count), rutina.Ejercicios.Select(e => e.Orden));
        }

        [Fact]
        public async Task SocioSinMedicion_LanzaReglaDeNegocio()
        {
            var repo = RepoCompleto();
            repo.Medicion = null;

            await Assert.ThrowsAsync<ReglaNegocioException>(() => Servicio(repo).GenerarAsync(SocioId));
        }

        [Fact]
        public async Task MedicionSinIMC_SeCalculaDesdePesoYAltura()
        {
            var repo = RepoCompleto();
            // Como las que carga la app: solo peso y altura (IMC = 0). 80 / 1,80^2 = 24,69 -> Normal.
            repo.Medicion = new MedicionFisica { SocioId = SocioId, PesoKg = 80m, AlturaCm = 180m, IMC = 0m };

            var rutina = await Servicio(repo).GenerarAsync(SocioId);

            Assert.Contains("IMC Normal", rutina.Observaciones);
        }

        [Fact]
        public async Task MedicionSinIMCNiPesoYAltura_LanzaReglaDeNegocio()
        {
            var repo = RepoCompleto();
            repo.Medicion = new MedicionFisica { SocioId = SocioId, IMC = 0m };

            await Assert.ThrowsAsync<ReglaNegocioException>(() => Servicio(repo).GenerarAsync(SocioId));
        }

        [Fact]
        public async Task SocioInexistente_LanzaRecursoNoEncontrado()
        {
            var repo = RepoCompleto();
            repo.SocioActual = null;

            await Assert.ThrowsAsync<RecursoNoEncontradoException>(() => Servicio(repo).GenerarAsync(SocioId));
        }

        [Fact]
        public async Task SocioSinConsentimiento_LanzaReglaDeNegocio()
        {
            var repo = RepoCompleto();
            repo.SocioActual = NuevoSocio(consentimiento: false);

            await Assert.ThrowsAsync<ReglaNegocioException>(() => Servicio(repo).GenerarAsync(SocioId));
        }

        [Fact]
        public async Task RutinaPendiente_ImpideGenerarOtra()
        {
            var repo = RepoCompleto();
            repo.TienePendiente = true;

            await Assert.ThrowsAsync<ReglaNegocioException>(() => Servicio(repo).GenerarAsync(SocioId));
        }

        [Fact]
        public async Task SinDosificaciones_LanzaCatalogosIncompletos()
        {
            var repo = RepoCompleto();
            repo.Dosificaciones.Clear();

            await Assert.ThrowsAsync<CatalogosIncompletosException>(() => Servicio(repo).GenerarAsync(SocioId));
        }

        [Fact]
        public async Task ZonaSinEjercicios_SeOmiteYQuedaConstanciaEnObservaciones()
        {
            var repo = RepoCompleto();
            repo.Ejercicios.RemoveAll(e => e.ZonaObjetivo == "Core");

            var rutina = await Servicio(repo).GenerarAsync(SocioId);

            Assert.Equal(4, rutina.Ejercicios.Count);
            Assert.Contains("Core", rutina.Observaciones);
        }

        [Fact]
        public async Task ZonaSinDosificacion_UsaDosisPorDefectoYLoAclara()
        {
            var repo = RepoCompleto();
            repo.Dosificaciones.RemoveAll(d => d.ZonaObjetivo == "Arms");

            var rutina = await Servicio(repo).GenerarAsync(SocioId);

            var brazos = rutina.Ejercicios.Single(e => e.Ejercicio.Contains("Arms"));
            Assert.Equal(3, brazos.SeriesObjetivo);
            Assert.Contains("Sin dosificación", brazos.Observaciones);
        }

        [Fact]
        public async Task Reproducibilidad_DosLlamadasGeneranLosMismosEjerciciosYVolumen()
        {
            var repo = RepoCompleto();
            var servicio = Servicio(repo);

            var primera = await servicio.GenerarAsync(SocioId);
            repo.TienePendiente = false; // la primera quedó pendiente; se simula su revisión
            var segunda = await servicio.GenerarAsync(SocioId);

            string Firma(RutinaPendienteDto r) => string.Join("|", r.Ejercicios.Select(e =>
                $"{e.Ejercicio}:{e.SeriesObjetivo}:{e.RepeticionesMinimas}:{e.RepeticionesMaximas}:{e.RIRObjetivo}:{e.DescansoObjetivoSegundos}"));
            Assert.Equal(Firma(primera), Firma(segunda));
        }

        [Fact]
        public async Task SeleccionaPrimeroPorDificultadYNombre_SinAleatoriedad()
        {
            var repo = RepoCompleto();

            var rutina = await Servicio(repo).GenerarAsync(SocioId);

            // En cada zona el candidato de dificultad 1 ("... A") precede al de dificultad 2 ("... B").
            Assert.All(rutina.Ejercicios, e => Assert.EndsWith(" A", e.Ejercicio));
        }

        [Theory]
        [InlineData(17.9, CategoriaIMC.BajoPeso)]
        [InlineData(18.5, CategoriaIMC.Normal)]
        [InlineData(24.9, CategoriaIMC.Normal)]
        [InlineData(26.0, CategoriaIMC.Sobrepeso)]
        [InlineData(30.0, CategoriaIMC.Obeso)]
        public void MapeoIMC_ClasificaSegunUmbralesOMS(double imc, CategoriaIMC esperada)
        {
            Assert.Equal(esperada, ClasificadorIMC.Clasificar((decimal)imc));
        }

        [Fact]
        public void CalculoIMC_PesoSobreAlturaAlCuadrado()
        {
            Assert.Equal(24.69m, ClasificadorIMC.Calcular(80m, 180m));
            Assert.Null(ClasificadorIMC.Calcular(0m, 180m));
        }

        [Theory]
        [InlineData("2000-06-15", "2026-06-14", 25)]
        [InlineData("2000-06-15", "2026-06-15", 26)]
        public void CalculoDeEdad_RespetaElCumpleaños(string nacimiento, string hoy, int esperada)
        {
            Assert.Equal(esperada, PrescripcionBaselineService.CalcularEdad(DateTime.Parse(nacimiento), DateTime.Parse(hoy)));
        }

        // ---- Etapa 9a-1: Core y tipo de medida ----

        /// <summary>Catálogo con las 5 zonas; Core con un ejercicio por segundos (dificultad 1) y su fila de segundos.</summary>
        private static FakeBaselineRepository RepoConCoreEnSegundos()
        {
            var repo = RepoCompleto();
            repo.Ejercicios.RemoveAll(e => e.ZonaObjetivo == "Core");
            repo.Ejercicios.Add(new EjercicioCandidato(Guid.NewGuid(), "Plancha", "Body Weight", "Core", "Recto abdominal", 1, TipoMedida.Segundos));
            repo.Ejercicios.Add(new EjercicioCandidato(Guid.NewGuid(), "Crunch", "Body Weight", "Core", "Recto abdominal", 2, TipoMedida.Repeticiones));
            repo.Dosificaciones.RemoveAll(d => d.ZonaObjetivo == "Core");
            repo.Dosificaciones.Add(new DosificacionVolumetrica
            {
                Id = Guid.NewGuid(), ZonaObjetivo = "Core", SeriesMinimas = 3, SeriesMaximas = 4,
                RepeticionesMinimas = 10, RepeticionesMaximas = 15, TipoMedida = TipoMedida.Repeticiones,
            });
            repo.Dosificaciones.Add(new DosificacionVolumetrica
            {
                Id = Guid.NewGuid(), ZonaObjetivo = "Core", SeriesMinimas = 2, SeriesMaximas = 3,
                RepeticionesMinimas = 20, RepeticionesMaximas = 40, TipoMedida = TipoMedida.Segundos,
            });
            return repo;
        }

        [Fact]
        public async Task ConLasCincoZonasEnElCatalogo_GeneraCincoEjercicios_YCoreNoFiguraComoOmitida()
        {
            var rutina = await Servicio(RepoConCoreEnSegundos()).GenerarAsync(SocioId);

            Assert.Equal(5, rutina.Ejercicios.Count);
            Assert.DoesNotContain("omitidas", rutina.Observaciones);
        }

        [Fact]
        public async Task EjercicioEnSegundos_TomaLaDosisDeLaFilaDeSegundos()
        {
            var repo = RepoConCoreEnSegundos();

            var rutina = await Servicio(repo).GenerarAsync(SocioId);

            var plancha = rutina.Ejercicios.Single(e => e.Ejercicio == "Plancha");
            Assert.Equal("Segundos", plancha.TipoMedida);
            Assert.Equal(2, plancha.SeriesObjetivo);
            Assert.Equal(20, plancha.RepeticionesMinimas);
            Assert.Equal(40, plancha.RepeticionesMaximas);
            Assert.Null(plancha.Observaciones);
            Assert.Contains(TipoMedida.Segundos, repo.TiposPedidos);
        }

        [Fact]
        public async Task EjercicioEnRepeticiones_TomaLaDosisDeLaFilaDeRepeticiones()
        {
            var repo = RepoConCoreEnSegundos();
            repo.Ejercicios.RemoveAll(e => e.Nombre == "Plancha"); // Core queda con el crunch (repeticiones)

            var rutina = await Servicio(repo).GenerarAsync(SocioId);

            var crunch = rutina.Ejercicios.Single(e => e.Ejercicio == "Crunch");
            Assert.Equal("Repeticiones", crunch.TipoMedida);
            Assert.Equal(3, crunch.SeriesObjetivo);
            Assert.Equal(10, crunch.RepeticionesMinimas);
            Assert.Equal(15, crunch.RepeticionesMaximas);
            Assert.All(rutina.Ejercicios.Where(e => e.Ejercicio != "Crunch"), e => Assert.Equal("Repeticiones", e.TipoMedida));
        }

        [Fact]
        public async Task SinFilaDeSegundos_UsaLaDosisPorDefectoDeSegundos_YLoDejaEnObservaciones()
        {
            var repo = RepoConCoreEnSegundos();
            repo.Dosificaciones.RemoveAll(d => d.TipoMedida == TipoMedida.Segundos);

            var rutina = await Servicio(repo).GenerarAsync(SocioId);

            var plancha = rutina.Ejercicios.Single(e => e.Ejercicio == "Plancha");
            Assert.Equal(3, plancha.SeriesObjetivo);
            Assert.Equal(20, plancha.RepeticionesMinimas);
            Assert.Equal(40, plancha.RepeticionesMaximas);
            Assert.Equal("Sin dosificación en el catálogo para la zona Core (Segundos): se usó 3 x 20-40 s.", plancha.Observaciones);
        }

        [Fact]
        public async Task SinFilaDeRepeticiones_UsaLaDosisPorDefectoDeRepeticiones_YLoDejaEnObservaciones()
        {
            var repo = RepoConCoreEnSegundos();
            repo.Ejercicios.RemoveAll(e => e.Nombre == "Plancha");
            repo.Dosificaciones.RemoveAll(d => d.ZonaObjetivo == "Core" && d.TipoMedida == TipoMedida.Repeticiones);

            var rutina = await Servicio(repo).GenerarAsync(SocioId);

            var crunch = rutina.Ejercicios.Single(e => e.Ejercicio == "Crunch");
            Assert.Equal((3, 8, 12), (crunch.SeriesObjetivo, crunch.RepeticionesMinimas, crunch.RepeticionesMaximas));
            Assert.Equal("Sin dosificación en el catálogo para la zona Core (Repeticiones): se usó 3 x 8-12 repeticiones.", crunch.Observaciones);
        }

        [Fact]
        public async Task LaObservacionGeneral_NoSuperaLos500Caracteres()
        {
            var rutina = await Servicio(RepoConCoreEnSegundos()).GenerarAsync(SocioId);
            Assert.True(rutina.Observaciones!.Length <= 500);
        }

        // ---- Etapa 9a-2: equipamiento del gimnasio ----

        private static readonly Guid GimnasioSocio = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid TipoMancuerna = CatalogoEquipamiento.IdDe(CatalogoEquipamiento.Mancuerna);
        private static readonly Guid TipoBarra = CatalogoEquipamiento.IdDe(CatalogoEquipamiento.Barra);
        private static readonly Guid TipoPesoCorporal = CatalogoEquipamiento.IdDe(CatalogoEquipamiento.PesoCorporal);

        /// <summary>Por zona: "A" con barra (dificultad 1, va primero) y "B" con mancuerna (dificultad 2). Core: solo peso corporal.</summary>
        private static FakeBaselineRepository RepoConEquipamiento()
        {
            var repo = new FakeBaselineRepository { SocioActual = NuevoSocio(), Medicion = NuevaMedicion() };
            repo.SocioActual.GimnasioId = GimnasioSocio;
            foreach (var z in new[] { "Chest", "Back", "Legs", "Arms" })
            {
                repo.Ejercicios.Add(new EjercicioCandidato(Guid.NewGuid(), $"{z} A barra", "Barbell", z, z, 1, TipoMedida.Repeticiones, TipoBarra));
                repo.Ejercicios.Add(new EjercicioCandidato(Guid.NewGuid(), $"{z} B mancuerna", "Dumbbell", z, z, 2, TipoMedida.Repeticiones, TipoMancuerna));
            }
            repo.Ejercicios.Add(new EjercicioCandidato(Guid.NewGuid(), "Core peso corporal", "Body Weight", "Core", "Core", 1, TipoMedida.Repeticiones, TipoPesoCorporal));
            repo.Dosificaciones.Add(new DosificacionVolumetrica { Id = Guid.NewGuid(), ZonaObjetivo = "Chest", SeriesMinimas = 3, RepeticionesMinimas = 8, RepeticionesMaximas = 12 });
            return repo;
        }

        [Fact]
        public async Task SinEquipamientoDeclarado_NoSeFiltra_YElResultadoEsElDeLa9a1()
        {
            var repo = RepoConEquipamiento();
            repo.Factibles = null;

            var rutina = await Servicio(repo).GenerarAsync(SocioId);

            Assert.Equal(new[] { "Chest A barra", "Back A barra", "Legs A barra", "Arms A barra", "Core peso corporal" },
                rutina.Ejercicios.Select(e => e.Ejercicio));
            Assert.DoesNotContain("equipamiento", rutina.Observaciones);
        }

        [Fact]
        public async Task ConEquipamientoDeclarado_SoloEligeEjerciciosFactibles_YElPesoCorporalSiempre()
        {
            var repo = RepoConEquipamiento();
            // Lo que devuelve el repositorio: lo marcado (mancuerna) más los siempre disponibles (peso corporal).
            repo.Factibles = new HashSet<Guid> { TipoMancuerna, TipoPesoCorporal };

            var rutina = await Servicio(repo).GenerarAsync(SocioId);

            Assert.Equal(new[] { "Chest B mancuerna", "Back B mancuerna", "Legs B mancuerna", "Arms B mancuerna", "Core peso corporal" },
                rutina.Ejercicios.Select(e => e.Ejercicio));
            Assert.Contains("Se usó el equipamiento declarado por el gimnasio.", rutina.Observaciones);
        }

        [Fact]
        public async Task ZonaSinEjerciciosFactibles_SeOmite_YLaObservacionLoDistingueDeLaZonaSinEjercicios()
        {
            var repo = RepoConEquipamiento();
            repo.Ejercicios.RemoveAll(e => e.ZonaObjetivo == "Arms"); // sin ejercicios en el catálogo
            repo.Factibles = new HashSet<Guid> { TipoPesoCorporal };  // declaró equipamiento, pero no barra ni mancuerna

            var rutina = await Servicio(repo).GenerarAsync(SocioId);

            Assert.Equal(new[] { "Core peso corporal" }, rutina.Ejercicios.Select(e => e.Ejercicio));
            Assert.Contains("Zonas sin ejercicios en el catálogo, omitidas: Brazos", rutina.Observaciones);
            Assert.Contains("Zonas omitidas por equipamiento (el gimnasio no declaró el necesario): Pecho, Espalda, Piernas", rutina.Observaciones);
            Assert.True(rutina.Observaciones!.Length <= 500);
        }

        [Fact]
        public async Task SiNingunaZonaEsFactible_PorEquipamiento_LanzaReglaDeNegocio()
        {
            var repo = RepoConEquipamiento();
            repo.Ejercicios.RemoveAll(e => e.ZonaObjetivo == "Core");
            repo.Factibles = new HashSet<Guid> { TipoPesoCorporal };

            await Assert.ThrowsAsync<ReglaNegocioException>(() => Servicio(repo).GenerarAsync(SocioId));
        }

        [Fact]
        public async Task SeConsultaElGimnasioDelSocio_YElSocioIndependienteNoSeFiltra()
        {
            var repo = RepoConEquipamiento();
            repo.Factibles = new HashSet<Guid> { TipoPesoCorporal };
            await Servicio(repo).GenerarAsync(SocioId);
            Assert.Equal(GimnasioSocio, repo.GimnasioConsultado);

            var independiente = RepoConEquipamiento();
            independiente.SocioActual!.GimnasioId = null;
            independiente.Factibles = new HashSet<Guid> { TipoPesoCorporal }; // no se consulta: el socio no tiene gimnasio
            var rutina = await Servicio(independiente).GenerarAsync(SocioId);
            Assert.Null(independiente.GimnasioConsultado);
            Assert.Equal(5, rutina.Ejercicios.Count);
        }

        // ---- Dobles de prueba ----
        private sealed class FakeBaselineRepository : IBaselineRepository
        {
            public Socio? SocioActual { get; set; }
            public MedicionFisica? Medicion { get; set; }
            public bool TienePendiente { get; set; }
            public List<EjercicioCandidato> Ejercicios { get; } = new();
            public List<DosificacionVolumetrica> Dosificaciones { get; } = new();
            private readonly List<PrescripcionEntrenamiento> _guardadas = new();

            public Task<Socio?> GetSocioAsync(Guid socioId) => Task.FromResult(SocioActual);
            public Task<MedicionFisica?> GetUltimaMedicionAsync(Guid socioId) => Task.FromResult(Medicion);

            public Task<MatrizRecomendacion?> BuscarMatrizAsync(string genero, int edad, bool hipertension, bool diabetes,
                CategoriaIMC categoriaImc, ObjetivoEntrenamiento objetivo) =>
                Task.FromResult<MatrizRecomendacion?>(new MatrizRecomendacion
                {
                    Genero = genero, Edad = edad, CategoriaRutina = "Rutina estándar", RecomendacionGeneral = "Hidratación adecuada",
                });

            public Task<bool> HayDosificacionesAsync() => Task.FromResult(Dosificaciones.Count > 0);

            public List<TipoMedida> TiposPedidos { get; } = new();

            public Task<DosificacionVolumetrica?> BuscarDosificacionAsync(IReadOnlyCollection<string> aliasZona, TipoMedida tipoMedida)
            {
                TiposPedidos.Add(tipoMedida);
                return Task.FromResult(Dosificaciones.FirstOrDefault(d =>
                    aliasZona.Contains(d.ZonaObjetivo, StringComparer.OrdinalIgnoreCase) && d.TipoMedida == tipoMedida));
            }

            /// <summary>Gimnasio pedido en la última consulta de ejercicios (Etapa 9a-2).</summary>
            public Guid? GimnasioConsultado { get; private set; }

            /// <summary>Tipos factibles que devuelve el repositorio; null = el gimnasio no declaró equipamiento.</summary>
            public HashSet<Guid>? Factibles { get; set; }

            public Task<IReadOnlySet<Guid>?> GetTiposEquipamientoFactiblesAsync(Guid gimnasioId) =>
                Task.FromResult<IReadOnlySet<Guid>?>(Factibles);

            public Task<IReadOnlyList<EjercicioCandidato>> GetEjerciciosDeZonaAsync(IReadOnlyCollection<string> aliasZona, Guid? gimnasioId)
            {
                GimnasioConsultado = gimnasioId;
                IReadOnlyList<EjercicioCandidato> lista = Ejercicios
                    .Where(e => aliasZona.Contains(e.ZonaObjetivo, StringComparer.OrdinalIgnoreCase))
                    .OrderBy(e => e.Dificultad).ThenBy(e => e.Nombre, StringComparer.Ordinal).ThenBy(e => e.Id)
                    .ToList();
                return Task.FromResult(lista);
            }

            public Task<bool> TienePendienteDeRevisionAsync(Guid socioId) => Task.FromResult(TienePendiente);

            public Task AddPrescripcionAsync(PrescripcionEntrenamiento prescripcion)
            {
                prescripcion.Socio = SocioActual!;
                foreach (var pe in prescripcion.EjerciciosPrescritos)
                {
                    var c = Ejercicios.First(e => e.Id == pe.EjercicioId);
                    pe.Ejercicio = new Ejercicio { Id = c.Id, Nombre = c.Nombre, Equipamiento = c.Equipamiento, ZonaObjetivo = c.ZonaObjetivo, TipoMedida = c.TipoMedida };
                }
                _guardadas.Add(prescripcion);
                return Task.CompletedTask;
            }

            public Task<IReadOnlyList<PrescripcionEntrenamiento>> GetPrescripcionesDeSocioAsync(Guid socioId)
            {
                IReadOnlyList<PrescripcionEntrenamiento> lista = _guardadas.OrderByDescending(p => p.FechaAlta).ToList();
                return Task.FromResult(lista);
            }

            public Task SaveChangesAsync() => Task.CompletedTask;
        }

        private sealed class FakeAcceso : ISocioAccessService
        {
            public Task AsegurarAccesoAsync(Guid socioId) => Task.CompletedTask;
        }

        private sealed class FakeRevision : IRevisionPrescripcionService
        {
            public Task AplicarPoliticaAsync(PrescripcionEntrenamiento prescripcion)
            {
                prescripcion.DefinirEstadoInicial(requiereRevisionHumana: true);
                return Task.CompletedTask;
            }
            public Task<IEnumerable<RutinaPendienteDto>> ListarPendientesAsync() => throw new NotSupportedException();
            public Task<RutinaPendienteDto> RevisarAsync(Guid prescripcionId, RevisarRutinaRequest request) => throw new NotSupportedException();
        }

        private sealed class FakeAudit : IAuditService
        {
            public Task RegistrarAsync(string accion, string entidad, string? entidadId = null, string? detalle = null) => Task.CompletedTask;
            public Task<IEnumerable<AuditLogDto>> ConsultarAsync(int cantidad, Guid? gimnasioId, DateTime? desdeUtc = null,
                DateTime? hastaUtc = null, string? accion = null, string? entidad = null) => throw new NotSupportedException();
        }
    }
}
