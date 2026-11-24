using GymBro.Application.Services;
using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Domain.Exceptions;
using GymBro.Domain.Repositories;
using GymBro.Domain.Rules;
using GymBro.Domain.Rules.Kpis;
using Xunit;

namespace GymBro.Tests
{
    /// <summary>
    /// Etapa 7d: KPIs K1 a K4 (funciones puras) con casos de verdad conocida, y el servicio de lectura con dobles.
    /// Datos SINTÉTICOS de prueba.
    /// </summary>
    public class KpisProgresionTests
    {
        private static readonly Guid Prensa = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
        private static readonly Guid Curl = Guid.Parse("00000000-0000-0000-0000-0000000000a2");
        private static readonly Guid Plancha = Guid.Parse("00000000-0000-0000-0000-0000000000a3");
        private static readonly Guid FueraProtocolo = Guid.Parse("00000000-0000-0000-0000-0000000000a4");
        private static readonly DateTime Lunes = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);

        private static SerieKpi S(decimal kg, int reps, int? rir = null, bool cal = false) => new(kg, reps, rir, cal);

        private static EjercicioSesionKpi E(Guid id, GrupoMuscularPiernas? grupo, params SerieKpi[] series) =>
            new(id, TipoMedida.Repeticiones, grupo, null, series);

        private static SesionKpi Sesion(int semana, params EjercicioSesionKpi[] ejercicios) =>
            new(Guid.NewGuid(), Lunes.AddDays(7 * semana), null, ejercicios, Array.Empty<LineaKpi>(), false, false);

        // ---- K1 ----

        [Fact]
        public void K1_recupera_una_progresion_de_2_5_kg_semanales()
        {
            var sesiones = Enumerable.Range(0, 4)
                .Select(i => Sesion(i, E(Prensa, GrupoMuscularPiernas.Cuadriceps, S(60, 12, cal: true), S(100 + 2.5m * i, 10), S(100 + 2.5m * i, 9))))
                .Reverse()   // el orden de entrada no importa: se ordena por inicio
                .ToList();
            var k1 = KpisProgresion.K1DeltaCargaMaxima(sesiones);
            Assert.Equal(new decimal?[] { null, 2.5m, 2.5m, 2.5m }, k1.Select(x => x.DeltaKg));
            Assert.Equal(new[] { 100m, 102.5m, 105m, 107.5m }, k1.Select(x => x.CargaMaximaKg));
        }

        [Fact]
        public void K1_estancamiento_da_delta_0_y_el_calentamiento_mas_pesado_no_cuenta()
        {
            var sesiones = new[]
            {
                Sesion(0, E(Prensa, GrupoMuscularPiernas.Cuadriceps, S(100, 10))),
                Sesion(1, E(Prensa, GrupoMuscularPiernas.Cuadriceps, S(140, 3, cal: true), S(100, 10))),
            };
            Assert.Equal(new decimal?[] { null, 0m }, KpisProgresion.K1DeltaCargaMaxima(sesiones).Select(x => x.DeltaKg));
        }

        [Fact]
        public void K1_toma_el_maximo_si_el_ejercicio_se_repite_y_excluye_segundos_y_peso_corporal()
        {
            var sesion = new SesionKpi(Guid.NewGuid(), Lunes, null, new[]
            {
                E(Prensa, null, S(100, 10)),
                E(Prensa, null, S(110, 6)),
                new EjercicioSesionKpi(Plancha, TipoMedida.Segundos, null, null, new[] { S(10, 45) }),
                E(Curl, null, S(0, 12)),
            }, Array.Empty<LineaKpi>(), false, false);
            var k1 = Assert.Single(KpisProgresion.K1DeltaCargaMaxima(new[] { sesion }));
            Assert.Equal((Prensa, 110m, (decimal?)null), (k1.EjercicioId, k1.CargaMaximaKg, k1.DeltaKg));
        }

        // ---- K2 ----

        [Fact]
        public void K2_suma_volumen_por_grupo_y_lo_fuera_del_protocolo_solo_al_total()
        {
            var sesion = new SesionKpi(Guid.NewGuid(), Lunes, null, new[]
            {
                E(Prensa, GrupoMuscularPiernas.Cuadriceps, S(50, 10, cal: true), S(100, 10), S(100, 8)),   // 1800
                E(Curl, GrupoMuscularPiernas.Isquiotibiales, S(40, 12), S(40, 12)),                         // 960
                E(FueraProtocolo, null, S(20, 10)),                                                         // 200
                new EjercicioSesionKpi(Plancha, TipoMedida.Segundos, GrupoMuscularPiernas.Pantorrillas, null, new[] { S(10, 45) }),
            }, Array.Empty<LineaKpi>(), false, false);
            var k2 = Assert.Single(KpisProgresion.K2VolumenEfectivo(new[] { sesion }));
            Assert.Equal(1800m, k2.PorGrupo.Single(g => g.Grupo == GrupoMuscularPiernas.Cuadriceps).VolumenKg);
            Assert.Equal(2, k2.PorGrupo.Single(g => g.Grupo == GrupoMuscularPiernas.Cuadriceps).SeriesEfectivas);
            Assert.Equal(960m, k2.PorGrupo.Single(g => g.Grupo == GrupoMuscularPiernas.Isquiotibiales).VolumenKg);
            Assert.Equal((0m, 1), (k2.PorGrupo.Single(g => g.Grupo == GrupoMuscularPiernas.Pantorrillas).VolumenKg,
                k2.PorGrupo.Single(g => g.Grupo == GrupoMuscularPiernas.Pantorrillas).SeriesEfectivas));
            Assert.Equal(2960m, k2.VolumenTotalKg);
            Assert.Equal(6, k2.SeriesEfectivasTotales);
        }

        [Fact]
        public void Una_sesion_solo_con_calentamiento_no_es_efectiva_y_no_cuenta_en_ningun_KPI()
        {
            var sesion = Sesion(0, E(Prensa, GrupoMuscularPiernas.Cuadriceps, S(50, 10, cal: true)));
            Assert.Empty(KpisProgresion.K1DeltaCargaMaxima(new[] { sesion }));
            Assert.Empty(KpisProgresion.K2VolumenEfectivo(new[] { sesion }));
            Assert.Empty(KpisProgresion.K3Adherencia(new[] { sesion }));
            Assert.Equal(0, KpisProgresion.K4IndiceSeguridad(new[] { sesion }, 0).SesionesEfectivas);
            Assert.Null(KpisProgresion.K4IndiceSeguridad(new[] { sesion }, 0).Indice);
        }

        // ---- K3 ----

        [Fact]
        public void K3_series_fuera_de_rango_y_RIR_con_nulos()
        {
            var linea = new LineaKpi(Guid.NewGuid(), Prensa, 4, 8, 12, 2);
            var sesion = new SesionKpi(Guid.NewGuid(), Lunes, Guid.NewGuid(), new[]
            {
                new EjercicioSesionKpi(Prensa, TipoMedida.Repeticiones, GrupoMuscularPiernas.Cuadriceps, linea, new[]
                {
                    S(60, 15, 5, cal: true),   // calentamiento: no cuenta
                    S(100, 12, 3),             // en rango, +1
                    S(100, 10, null),          // en rango, sin RIR
                    S(100, 7, 0),              // fuera de rango, -2
                    S(100, 13, 1),             // fuera de rango, -1
                }),
                E(Curl, GrupoMuscularPiernas.Isquiotibiales, S(40, 10, 2)),   // sin línea vinculada
            }, new[] { linea }, false, false);

            var k3 = KpisProgresion.K3Adherencia(new[] { sesion });
            var prensa = k3.Single(x => x.EjercicioId == Prensa);
            Assert.Equal(50m, prensa.PorcentajeEnRango);
            Assert.Equal(4, prensa.NRango);
            Assert.Equal(-2m / 3m, prensa.DesvioRirMedio);       // (1 - 2 - 1) / 3
            Assert.Equal(4m / 3m, prensa.DesvioRirAbsolutoMedio); // (1 + 2 + 1) / 3
            Assert.Equal(3, prensa.NRir);
            Assert.Equal(linea.Id, prensa.PrescripcionEjercicioId);

            var curl = k3.Single(x => x.EjercicioId == Curl);
            Assert.Null(curl.PorcentajeEnRango);
            Assert.Null(curl.DesvioRirMedio);
            Assert.Null(curl.PrescripcionEjercicioId);
        }

        // ---- K4 ----

        [Fact]
        public void K4_sesiones_limpias_sobre_efectivas_con_alerta_sugerencia_y_series_faltantes()
        {
            var linea = new LineaKpi(Guid.NewGuid(), Prensa, 2, 8, 12, 2);
            EjercicioSesionKpi Hecho(int series) => new(Prensa, TipoMedida.Repeticiones, GrupoMuscularPiernas.Cuadriceps, linea,
                Enumerable.Range(0, series).Select(_ => S(100, 10, 2)).ToList());
            SesionKpi Con(int dia, int series, bool alerta = false, bool bajar = false) =>
                new(Guid.NewGuid(), Lunes.AddDays(dia), Guid.NewGuid(), new[] { Hecho(series) }, new[] { linea }, alerta, bajar);

            var sesiones = new[]
            {
                Con(0, 2),                 // limpia
                Con(1, 3),                 // limpia (más series que las prescritas)
                Con(2, 2, alerta: true),   // alerta asociada
                Con(3, 2, bajar: true),    // sugerencia R-PROG-1 de bajar carga
                Con(4, 1),                 // faltó una serie
                Sesion(5, E(Curl, null, S(40, 10))),   // sin prescripción: limpia
            };
            var k4 = KpisProgresion.K4IndiceSeguridad(sesiones, generacionesConReduccionPorAlerta: 2);
            Assert.Equal((6, 3), (k4.SesionesEfectivas, k4.SesionesLimpias));
            Assert.Equal(0.5m, k4.Indice);
            Assert.Equal(2, k4.GeneracionesConReduccionPorAlerta);
            Assert.Equal(new bool?[] { true, true, true, true, false, null }, k4.Detalle.Select(d => d.CumpleSeriesPrescritas));
        }

        [Fact]
        public void K4_una_linea_de_la_rutina_que_no_se_hizo_deja_la_sesion_no_limpia()
        {
            var a = new LineaKpi(Guid.NewGuid(), Prensa, 2, 8, 12, 2);
            var b = new LineaKpi(Guid.NewGuid(), Curl, 2, 8, 12, 2);
            var sesion = new SesionKpi(Guid.NewGuid(), Lunes, Guid.NewGuid(),
                new[] { new EjercicioSesionKpi(Prensa, TipoMedida.Repeticiones, null, a, new[] { S(100, 10), S(100, 10) }) },
                new[] { a, b }, false, false);
            Assert.False(KpisProgresion.K4IndiceSeguridad(new[] { sesion }, 0).Detalle.Single().Limpia);
        }

        // ---- Servicio: armado de entradas y exclusión de datos no sintéticos ----

        [Theory]
        [InlineData(OrigenDatos.Real, false, false)]
        [InlineData(OrigenDatos.DatasetExterno, false, false)]
        [InlineData(OrigenDatos.ValidacionFuncional, false, false)]
        [InlineData(OrigenDatos.ValidacionFuncional, true, true)]
        [InlineData(OrigenDatos.Sintetico, false, true)]
        public async Task El_servicio_solo_incluye_datos_sinteticos_salvo_pedido_expreso_de_validacion_funcional(
            OrigenDatos origen, bool incluirValidacion, bool incluido)
        {
            var repo = new RepoKpiDoble(origen);
            var dto = await new KpiProgresionService(repo, new AccesoDoble()).CalcularAsync(repo.Socio.Id, null, null, incluirValidacion);
            Assert.Equal(incluido, dto.Incluido);
            Assert.Equal(incluido ? 1 : 0, dto.K1DeltaCargaMaxima.Count);
            if (!incluido) Assert.Contains("sintéticos", dto.Motivo);
        }

        [Fact]
        public async Task El_servicio_excluye_las_rutinas_marcadas_excluida_estudio_y_asocia_alertas()
        {
            var repo = new RepoKpiDoble(OrigenDatos.Sintetico);
            var excluida = new PrescripcionEntrenamiento { SocioId = repo.Socio.Id, ExcluidaEstudio = true };
            repo.Sesiones.Add(new SesionEntrenamiento
            {
                Id = Guid.NewGuid(), SocioId = repo.Socio.Id, Inicio = Lunes.AddDays(1), PrescripcionEntrenamiento = excluida,
                PrescripcionEntrenamientoId = excluida.Id,
                EjerciciosEjecutados = { new SesionEjercicio { EjercicioId = Prensa, Orden = 1, SeriesRealizadas = { new SerieRealizada { OrdenSerie = 1, PesoLevantadoKg = 500, Repeticiones = 1 } } } },
            });
            repo.Alertas.Add(new AlertaSeguridad
            {
                SocioId = repo.Socio.Id, SesionId = repo.Sesiones[0].Id, Tipo = TipoAlerta.Alerta, ReglaCodigo = CodigosRegla.RirMayorAlPlanificado,
            });

            var dto = await new KpiProgresionService(repo, new AccesoDoble()).CalcularAsync(repo.Socio.Id, null, null, false);
            Assert.Equal(1, dto.SesionesExcluidas);
            Assert.Equal(100m, Assert.Single(dto.K1DeltaCargaMaxima).CargaMaximaKg);
            Assert.Equal((1, 0), (dto.K4IndiceSeguridad!.SesionesEfectivas, dto.K4IndiceSeguridad.SesionesLimpias));
        }

        [Fact]
        public async Task El_servicio_rechaza_un_periodo_invertido()
        {
            var repo = new RepoKpiDoble(OrigenDatos.Sintetico);
            await Assert.ThrowsAsync<ReglaNegocioException>(() =>
                new KpiProgresionService(repo, new AccesoDoble()).CalcularAsync(repo.Socio.Id, Lunes, Lunes.AddDays(-1), false));
        }

        [Fact]
        public void ArmarSesion_prefiere_el_vinculo_directo_y_si_falta_usa_ejercicio_y_orden()
        {
            var rutina = new PrescripcionEntrenamiento();
            var l1 = new PrescripcionEjercicio { EjercicioId = Prensa, Orden = 1, SeriesObjetivo = 3, RepeticionesMinimas = 8, RepeticionesMaximas = 12 };
            var l2 = new PrescripcionEjercicio { EjercicioId = Curl, Orden = 2, SeriesObjetivo = 2, RepeticionesMinimas = 10, RepeticionesMaximas = 15 };
            rutina.EjerciciosPrescritos.Add(l1);
            rutina.EjerciciosPrescritos.Add(l2);
            var sesion = new SesionEntrenamiento
            {
                Id = Guid.NewGuid(), Inicio = Lunes, PrescripcionEntrenamiento = rutina, PrescripcionEntrenamientoId = rutina.Id,
                EjerciciosEjecutados =
                {
                    new SesionEjercicio { EjercicioId = Prensa, Orden = 5, PrescripcionEjercicioId = l1.Id },   // vínculo directo, otro orden
                    new SesionEjercicio { EjercicioId = Curl, Orden = 2 },                                       // sesión anterior: por ejercicio y orden
                },
            };
            var kpi = KpiProgresionService.ArmarSesion(sesion, new Dictionary<Guid, GrupoMuscularPiernas>(),
                Array.Empty<AlertaSeguridad>(), new Dictionary<Guid, DateOnly>());
            Assert.Equal(l1.Id, kpi.Ejercicios.Single(e => e.EjercicioId == Prensa).Linea!.Id);
            Assert.Equal(l2.Id, kpi.Ejercicios.Single(e => e.EjercicioId == Curl).Linea!.Id);
        }

        [Fact]
        public void ArmarSesion_asocia_R_FC_1_por_el_dia_local_y_la_sugerencia_de_bajar_carga()
        {
            var sesion = new SesionEntrenamiento { Id = Guid.NewGuid(), Inicio = new DateTime(2026, 11, 3, 2, 0, 0, DateTimeKind.Utc) };   // 2/11 23 h en Argentina
            var registro = Guid.NewGuid();
            var fc = new AlertaSeguridad { Tipo = TipoAlerta.Alerta, ReglaCodigo = CodigosRegla.FcReposo, RegistroBienestarId = registro };
            var bajar = new AlertaSeguridad
            {
                Tipo = TipoAlerta.Sugerencia, ReglaCodigo = CodigosRegla.Progresion, SesionId = sesion.Id,
                Detalle = "{\"resultado\":{\"direccion\":\"Bajar\"}}",
            };
            var mismoDia = KpiProgresionService.ArmarSesion(sesion, new Dictionary<Guid, GrupoMuscularPiernas>(), new[] { fc, bajar },
                new Dictionary<Guid, DateOnly> { [registro] = new DateOnly(2026, 11, 2) });
            Assert.True(mismoDia.AlertaAsociada);
            Assert.True(mismoDia.SugerenciaBajarCarga);

            var otroDia = KpiProgresionService.ArmarSesion(sesion, new Dictionary<Guid, GrupoMuscularPiernas>(), new[] { fc },
                new Dictionary<Guid, DateOnly> { [registro] = new DateOnly(2026, 11, 3) });
            Assert.False(otroDia.AlertaAsociada);
            Assert.False(otroDia.SugerenciaBajarCarga);
        }

        private sealed class AccesoDoble : ISocioAccessService
        {
            public Task AsegurarAccesoAsync(Guid socioId) => Task.CompletedTask;
        }

        private sealed class RepoKpiDoble : IKpiProgresionRepository
        {
            public Socio Socio { get; }
            public List<SesionEntrenamiento> Sesiones { get; } = new();
            public List<AlertaSeguridad> Alertas { get; } = new();

            public RepoKpiDoble(OrigenDatos origen)
            {
                Socio = new Socio { Id = Guid.NewGuid(), OrigenDatos = origen };
                Sesiones.Add(new SesionEntrenamiento
                {
                    Id = Guid.NewGuid(), SocioId = Socio.Id, Inicio = Lunes,
                    EjerciciosEjecutados = { new SesionEjercicio { EjercicioId = Prensa, Orden = 1, SeriesRealizadas = { new SerieRealizada { OrdenSerie = 1, PesoLevantadoKg = 100, Repeticiones = 10 } } } },
                });
            }

            public Task<Socio?> GetSocioAsync(Guid socioId) => Task.FromResult<Socio?>(Socio);
            public Task<IReadOnlyList<SesionEntrenamiento>> GetSesionesAsync(Guid socioId, DateTime? desdeUtc, DateTime? hastaUtc) =>
                Task.FromResult<IReadOnlyList<SesionEntrenamiento>>(Sesiones);
            public Task<IReadOnlyDictionary<Guid, GrupoMuscularPiernas>> GetGruposProtocoloAsync(string protocoloCodigo) =>
                Task.FromResult<IReadOnlyDictionary<Guid, GrupoMuscularPiernas>>(new Dictionary<Guid, GrupoMuscularPiernas> { [Prensa] = GrupoMuscularPiernas.Cuadriceps });
            public Task<IReadOnlyList<AlertaSeguridad>> GetAlertasVigentesAsync(Guid socioId) => Task.FromResult<IReadOnlyList<AlertaSeguridad>>(Alertas);
            public Task<IReadOnlyDictionary<Guid, DateOnly>> GetFechasBienestarAsync(Guid socioId) =>
                Task.FromResult<IReadOnlyDictionary<Guid, DateOnly>>(new Dictionary<Guid, DateOnly>());
            public Task<IReadOnlyList<PrescripcionEntrenamiento>> GetPrescripcionesProtocoloAsync(Guid socioId, string protocoloCodigo,
                DateTime? desdeUtc, DateTime? hastaUtc) => Task.FromResult<IReadOnlyList<PrescripcionEntrenamiento>>(Array.Empty<PrescripcionEntrenamiento>());
        }
    }
}
