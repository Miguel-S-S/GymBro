using GymBro.Application.Services;
using GymBro.Application.Sintetico;
using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Infrastructure.Sintetico;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace GymBro.Tests.Integration
{
    /// <summary>API con el modo generador (identidad y reloj simulados), contra PostgreSQL descartable.</summary>
    public class GymBroApiFactorySintetico : GymBroApiFactory
    {
        protected override void ConfigurarExtra(IWebHostBuilder builder) =>
            builder.ConfigureTestServices(s => s.AgregarModoSintetico());
    }

    /// <summary>
    /// Etapa 7e: el generador sintetico es reproducible (misma semilla y parametros, mismo resumen SHA-256), la eliminacion
    /// solo toca datos sinteticos de gimnasios sinteticos, y K1 a K4 calculados con el servicio real recuperan los
    /// parametros conocidos dentro de tolerancias declaradas de antemano (docs/pfc/sintetico-parametros.md). Datos SINTETICOS.
    /// </summary>
    public class GeneradorSinteticoTests : IClassFixture<GeneradorSinteticoTests.Escenario>
    {
        // Tolerancias declaradas antes de ejecutar (ver el documento de parametros).
        public const double ToleranciaRir = 0.15;
        public const double ToleranciaRango = 0.05;
        public const double ToleranciaGanancia = 0.005;
        public const double ToleranciaCompletitud = 0.08;

        private readonly Escenario _e;
        private readonly ITestOutputHelper _salida;
        public GeneradorSinteticoTests(Escenario e, ITestOutputHelper salida) { _e = e; _salida = salida; }

        [Fact]
        public void Misma_semilla_y_parametros_producen_el_mismo_resumen()
        {
            _salida.WriteLine($"Semilla 7, primera: {_e.Primera.Resumen}");
            _salida.WriteLine($"Semilla 7, segunda: {_e.Segunda.Resumen}");
            Assert.Equal(_e.Primera.Resumen, _e.Segunda.Resumen);
            Assert.Equal(_e.Primera.SociosIds, _e.Segunda.SociosIds);
            Assert.True(_e.Primera.Resumen.Sesiones > 50);
            Assert.True(_e.Primera.Resumen.Bienestar > 100);
            Assert.True(_e.Primera.Resumen.Asistencias == _e.Primera.Resumen.Sesiones);
            Assert.True(_e.Primera.Resumen.Cuotas > 0);
        }

        [Fact]
        public void Otra_semilla_produce_otros_datos_y_otros_ids()
        {
            _salida.WriteLine($"Semilla 8: {_e.OtraSemilla.Resumen}");
            Assert.NotEqual(_e.Primera.Resumen.Sha256, _e.OtraSemilla.Resumen.Sha256);
            Assert.Empty(_e.Primera.SociosIds.Intersect(_e.OtraSemilla.SociosIds));
        }

        [Fact]
        public void La_eliminacion_borra_solo_lo_sintetico_de_gimnasios_sinteticos()
        {
            _salida.WriteLine($"Eliminacion intermedia: {_e.EliminacionIntermedia}; final: {_e.EliminacionFinal}");
            Assert.Equal(_e.Primera.Resumen.Socios, _e.EliminacionIntermedia.Socios);
            Assert.Equal(_e.Primera.Resumen.Sesiones, _e.EliminacionIntermedia.Sesiones);
            Assert.Equal(1, _e.EliminacionIntermedia.Gimnasios);
            Assert.Equal(2, _e.EliminacionFinal.Gimnasios);
            Assert.Equal(0, _e.RestantesSinteticosEnGimnasiosSint);
            // Lo ajeno sigue: el socio Real de otro gimnasio y el socio Sintetico de un gimnasio que no es sintetico.
            Assert.Equal((1, 1, 1), _e.AjenosDespues);
        }

        [Fact]
        public void El_generador_crea_solo_socios_Sintetico_con_rutinas_Sintetico()
        {
            Assert.Equal(0, _e.NoSinteticosGenerados);
        }

        [Fact]
        public void K3_recupera_el_porcentaje_en_rango_y_el_sesgo_de_RIR()
        {
            _salida.WriteLine($"K3: en rango observado {_e.RangoObservado:F4} vs esperado {_e.RangoEsperado:F4} (n = {_e.NRango}); " +
                              $"desvio RIR observado {_e.RirObservado:F4} vs esperado {_e.RirEsperado:F4} (n = {_e.NRir})");
            Assert.InRange(_e.RangoObservado, _e.RangoEsperado - ToleranciaRango, _e.RangoEsperado + ToleranciaRango);
            Assert.InRange(_e.RirObservado, _e.RirEsperado - ToleranciaRir, _e.RirEsperado + ToleranciaRir);
        }

        [Fact]
        public void K1_recupera_la_ganancia_semanal_de_carga_por_nivel()
        {
            Assert.NotEmpty(_e.GananciaPorNivel);
            foreach (var (nivel, (observada, n)) in _e.GananciaPorNivel)
            {
                var esperada = new ParametrosSinteticos().GananciaSemanalPorNivel[(int)nivel - 1];
                _salida.WriteLine($"K1 {nivel}: ganancia semanal observada {observada:F4} vs parametro {esperada:F4} (n = {n} pendientes)");
                Assert.InRange(observada, esperada - ToleranciaGanancia, esperada + ToleranciaGanancia);
            }
        }

        [Fact]
        public void K4_recupera_la_probabilidad_de_completar_las_series_y_K2_coincide_con_las_series()
        {
            _salida.WriteLine($"K4: series completas observadas {_e.CompletitudObservada:F4} vs parametro {new ParametrosSinteticos().ProbabilidadSeriesCompletas:F4} " +
                              $"(n = {_e.NSesionesK4}); indice de seguridad agregado {_e.IndiceSeguridad:F4}");
            Assert.InRange(_e.CompletitudObservada, new ParametrosSinteticos().ProbabilidadSeriesCompletas - ToleranciaCompletitud,
                new ParametrosSinteticos().ProbabilidadSeriesCompletas + ToleranciaCompletitud);
            Assert.Equal(_e.VolumenK2, _e.VolumenSeries);
        }

        public class Escenario : IAsyncLifetime
        {
            public GymBroApiFactorySintetico Api { get; } = new();
            public ResultadoGeneracion Primera { get; private set; } = null!;
            public ResultadoGeneracion Segunda { get; private set; } = null!;
            public ResultadoGeneracion OtraSemilla { get; private set; } = null!;
            public EliminacionSintetica EliminacionIntermedia { get; private set; } = null!;
            public EliminacionSintetica EliminacionFinal { get; private set; } = null!;
            public int RestantesSinteticosEnGimnasiosSint { get; private set; } = -1;
            public (int, int, int) AjenosDespues { get; private set; }
            public int NoSinteticosGenerados { get; private set; } = -1;
            public double RangoObservado, RangoEsperado, RirObservado, RirEsperado, CompletitudObservada, IndiceSeguridad;
            public int NRango, NRir, NSesionesK4;
            public decimal VolumenK2, VolumenSeries;
            public Dictionary<NivelExperiencia, (double Observada, int N)> GananciaPorNivel { get; } = new();

            private readonly Guid _socioReal = Guid.NewGuid(), _socioSintAjeno = Guid.NewGuid();
            private readonly Gimnasio _otro = new() { Nombre = "Otro gimnasio (prueba 7e)", Codigo = "OTRO-7E" };

            /// <summary>
            /// Tamano de la prueba: 12 socios y 8 semanas para que la suite sea rapida. Las variables de entorno
            /// GYMBRO_SINTETICO_SOCIOS y GYMBRO_SINTETICO_SEMANAS permiten repetirla con el tamano por defecto del generador
            /// (60 y 12) para la evidencia; las tolerancias no cambian.
            /// </summary>
            public static ParametrosSinteticos Parametros(int semilla) => new()
            {
                Semilla = semilla,
                Socios = int.TryParse(Environment.GetEnvironmentVariable("GYMBRO_SINTETICO_SOCIOS"), out var n) ? n : 12,
                Semanas = int.TryParse(Environment.GetEnvironmentVariable("GYMBRO_SINTETICO_SEMANAS"), out var s) ? s : 8,
            };

            public async Task InitializeAsync()
            {
                await Api.InitializeAsync();
                await SembrarProtocoloYAjenosAsync();
                var generador = Api.Services.GetRequiredService<GeneradorSintetico>();

                Primera = await generador.GenerarAsync(Parametros(7));
                EliminacionIntermedia = await generador.EliminarAsync();
                Segunda = await generador.GenerarAsync(Parametros(7));
                OtraSemilla = await generador.GenerarAsync(Parametros(8));

                await Api.ConDbAsync(async db =>
                {
                    var ids = Segunda.SociosIds.Concat(OtraSemilla.SociosIds).ToList();
                    NoSinteticosGenerados = await db.Socios.IgnoreQueryFilters().CountAsync(s => ids.Contains(s.Id) && s.OrigenDatos != OrigenDatos.Sintetico)
                        + await db.PrescripcionesEntrenamiento.IgnoreQueryFilters().CountAsync(p => ids.Contains(p.SocioId) && p.OrigenDatos != OrigenDatos.Sintetico)
                        + await db.Socios.IgnoreQueryFilters().CountAsync(s => s.GimnasioId == Segunda.GimnasioId && s.OrigenDatos != OrigenDatos.Sintetico);
                });
                await CalcularKpisAsync(Segunda, Parametros(7));

                EliminacionFinal = await generador.EliminarAsync();
                await Api.ConDbAsync(async db =>
                {
                    RestantesSinteticosEnGimnasiosSint = await db.Socios.IgnoreQueryFilters()
                        .CountAsync(s => s.Gimnasio != null && s.Gimnasio.Codigo.StartsWith("SINT"))
                        + await db.Gimnasios.IgnoreQueryFilters().CountAsync(g => g.Codigo.StartsWith("SINT"));
                    AjenosDespues = (
                        await db.Socios.IgnoreQueryFilters().CountAsync(s => s.Id == _socioReal),
                        await db.Socios.IgnoreQueryFilters().CountAsync(s => s.Id == _socioSintAjeno),
                        await db.Gimnasios.IgnoreQueryFilters().CountAsync(g => g.Id == _otro.Id));
                });
            }

            private async Task CalcularKpisAsync(ResultadoGeneracion r, ParametrosSinteticos p)
            {
                var ctx = Api.Services.GetRequiredService<ContextoSintetico>();
                ctx.ComoPersonal(RolUsuario.AdministradorGimnasio, AzarDeterminista.IdDerivado(p.Semilla, "dueno"), r.GimnasioId);
                double sumRango = 0, sumRangoEsp = 0, sumRir = 0, sumRirEsp = 0;
                var completas = 0;
                var pendientes = new Dictionary<NivelExperiencia, List<double>>();
                double limpias = 0, efectivas = 0;
                foreach (var socioId in r.SociosIds)
                {
                    using var scope = Api.Services.CreateScope();
                    var kpi = await scope.ServiceProvider.GetRequiredService<IKpiProgresionService>().CalcularAsync(socioId, null, null, false);
                    var db = scope.ServiceProvider.GetRequiredService<GymBro.Infrastructure.Persistence.GymBroDbContext>();
                    var lineas = await db.PrescripcionesEjercicios.IgnoreQueryFilters().AsNoTracking()
                        .Where(l => l.PrescripcionEntrenamiento.SocioId == socioId)
                        .ToDictionaryAsync(l => l.Id, l => (l.RepeticionesMinimas, l.RepeticionesMaximas, l.RIRObjetivo));
                    var nivel = (await db.Socios.IgnoreQueryFilters().SingleAsync(s => s.Id == socioId)).Nivel;

                    foreach (var a in kpi.K3Adherencia.Where(a => a.PrescripcionEjercicioId is not null))
                    {
                        var (min, max, rirObj) = lineas[a.PrescripcionEjercicioId!.Value];
                        if (a.PorcentajeEnRango is decimal pr)
                        {
                            sumRango += (double)pr / 100.0 * a.NRango;
                            sumRangoEsp += ModeloSintetico.ProbabilidadRepeticionesEnRango(min, max, p) * a.NRango;
                            NRango += a.NRango;
                        }
                        if (a.DesvioRirMedio is decimal dr && rirObj is int ro)
                        {
                            sumRir += (double)dr * a.NRir;
                            sumRirEsp += ModeloSintetico.EsperanzaDesvioRirMarginal(ro, p) * a.NRir;
                            NRir += a.NRir;
                        }
                    }

                    // K1: pendiente de ln(carga maxima) contra semanas, por ejercicio con al menos 4 apariciones.
                    foreach (var g in kpi.K1DeltaCargaMaxima.GroupBy(d => d.EjercicioId).Where(g => g.Count() >= 4))
                    {
                        var xs = g.Select(d => (d.Inicio.Date - p.Inicio.ToDateTime(TimeOnly.MinValue)).TotalDays / 7.0).ToArray();
                        var ys = g.Select(d => Math.Log((double)d.CargaMaximaKg)).ToArray();
                        var mx = xs.Average(); var my = ys.Average();
                        var den = xs.Sum(x => (x - mx) * (x - mx));
                        if (den <= 0) continue;
                        var pendiente = xs.Zip(ys).Sum(t => (t.First - mx) * (t.Second - my)) / den;
                        (pendientes.TryGetValue(nivel, out var l) ? l : pendientes[nivel] = new List<double>()).Add(Math.Exp(pendiente) - 1);
                    }

                    if (kpi.K4IndiceSeguridad is { } k4)
                    {
                        foreach (var d in k4.Detalle.Where(d => d.CumpleSeriesPrescritas is not null))
                        {
                            NSesionesK4++;
                            if (d.CumpleSeriesPrescritas == true) completas++;
                        }
                        limpias += k4.SesionesLimpias;
                        efectivas += k4.SesionesEfectivas;
                    }

                    VolumenK2 += kpi.K2VolumenEfectivo.Sum(v => v.VolumenTotalKg);
                    VolumenSeries += await db.SeriesRealizadas.IgnoreQueryFilters()
                        .Where(s => !s.EsCalentamiento && s.SesionEjercicio.Sesion.SocioId == socioId)
                        .SumAsync(s => s.PesoLevantadoKg * s.Repeticiones);
                }
                ctx.ComoSistema();
                RangoObservado = sumRango / NRango; RangoEsperado = sumRangoEsp / NRango;
                RirObservado = sumRir / NRir; RirEsperado = sumRirEsp / NRir;
                CompletitudObservada = completas / (double)NSesionesK4;
                IndiceSeguridad = efectivas == 0 ? 0 : limpias / efectivas;
                foreach (var (nivel, lista) in pendientes)
                {
                    var ordenada = lista.OrderBy(x => x).ToList();
                    var mediana = ordenada.Count % 2 == 1 ? ordenada[ordenada.Count / 2] : (ordenada[ordenada.Count / 2 - 1] + ordenada[ordenada.Count / 2]) / 2;
                    GananciaPorNivel[nivel] = (mediana, ordenada.Count);
                }
            }

            /// <summary>Catalogo del protocolo (forma de la siembra piernas-1.0) y datos ajenos que la eliminacion no debe tocar.</summary>
            private async Task SembrarProtocoloYAjenosAsync()
            {
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
                    var i = 0;
                    foreach (var f in filas)
                    {
                        // Ids fijos: el resumen depende de los ejercicios elegidos y debe ser igual en cada corrida de la prueba.
                        var id = new Guid($"7e000000-0000-0000-0000-{++i:D12}");
                        db.Ejercicios.Add(new Ejercicio { Id = id, Nombre = $"{f.Clave} (piernas sintetico)", ZonaObjetivo = "Legs", Origen = OrigenEjercicio.Proyecto });
                        db.EjerciciosProtocolo.Add(new EjercicioProtocolo
                        {
                            ProtocoloCodigo = "PIERNAS", ProtocoloVersion = "piernas-1.0", EjercicioId = id, Grupo = f.G, Rol = f.R, Clase = f.C,
                            RodillaExcluirDesde = f.Rod, LumbarExcluirDesde = f.Lum, OrdenRotacion = f.O,
                        });
                    }
                    db.Gimnasios.Add(_otro);
                    db.Socios.Add(Ajeno(_socioReal, "real", OrigenDatos.Real));
                    db.Socios.Add(Ajeno(_socioSintAjeno, "sintajeno", OrigenDatos.Sintetico));
                    return Task.CompletedTask;
                });
            }

            private Socio Ajeno(Guid id, string nombre, OrigenDatos origen) => new()
            {
                Id = id, Nombre = nombre, Apellido = "Ajeno", Email = $"{nombre}@ajeno7e.test", Genero = "F",
                FechaNacimiento = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc), GimnasioId = _otro.Id,
                Objetivo = ObjetivoEntrenamiento.Mantenimiento, Nivel = NivelExperiencia.Intermedio, OrigenDatos = origen,
            };

            public async Task DisposeAsync() => await Api.DisposeAsync();
        }
    }
}
