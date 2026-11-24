using System.Text.Json;
using GymBro.Domain.Enums;
using GymBro.Domain.Exceptions;
using GymBro.Domain.Rules;
using Xunit;

namespace GymBro.Tests
{
    /// <summary>
    /// Etapa 7d: PlanificadorPiernas (función pura). Catálogo SINTÉTICO con la misma forma que la siembra piernas-1.0
    /// (grupos, roles, clases, exclusiones y orden), con ids fijos de prueba. Los valores de la dosis son los provisionales
    /// de ProtocoloPiernasConfig.
    /// </summary>
    public class PlanificadorPiernasTests
    {
        public static readonly Guid Prensa = new("00000000-0000-0000-0000-000000000001");
        public static readonly Guid Hack = new("00000000-0000-0000-0000-000000000002");
        public static readonly Guid Trasera = new("00000000-0000-0000-0000-000000000003");
        public static readonly Guid Frontal = new("00000000-0000-0000-0000-000000000004");
        public static readonly Guid Extension = new("00000000-0000-0000-0000-000000000005");
        public static readonly Guid CurlSentado = new("00000000-0000-0000-0000-000000000006");
        public static readonly Guid CurlAcostado = new("00000000-0000-0000-0000-000000000007");
        public static readonly Guid PesoMuerto = new("00000000-0000-0000-0000-000000000008");
        public static readonly Guid TalonesPie = new("00000000-0000-0000-0000-000000000009");
        public static readonly Guid TalonesSentado = new("00000000-0000-0000-0000-00000000000a");

        private const GrupoMuscularPiernas Cuad = GrupoMuscularPiernas.Cuadriceps;
        private const GrupoMuscularPiernas Isq = GrupoMuscularPiernas.Isquiotibiales;
        private const GrupoMuscularPiernas Pan = GrupoMuscularPiernas.Pantorrillas;
        private const RolEjercicioProtocolo Ref = RolEjercicioProtocolo.Referencia;
        private const RolEjercicioProtocolo Var = RolEjercicioProtocolo.Variable;
        private const ClaseEjercicio Multi = ClaseEjercicio.Multiarticular;
        private const ClaseEjercicio Aisl = ClaseEjercicio.Aislamiento;
        private const NivelLimitacion Mod = NivelLimitacion.Moderada;
        private const NivelLimitacion Alta = NivelLimitacion.Alta;
        private const NivelLimitacion Sin = NivelLimitacion.SinLimitacion;

        /// <summary>Los 10 ejercicios con la forma de la siembra (especificación 4.2).</summary>
        public static List<CandidatoProtocolo> Catalogo(Func<Guid, bool>? factible = null)
        {
            bool F(Guid id) => factible?.Invoke(id) ?? true;
            return new List<CandidatoProtocolo>
            {
                new(Prensa, "Prensa", Cuad, Ref, Multi, Alta, Alta, 1, F(Prensa)),
                new(Hack, "Hack", Cuad, Ref, Multi, Alta, null, 2, F(Hack)),
                new(Trasera, "Trasera", Cuad, Var, Multi, Alta, Mod, 1, F(Trasera)),
                new(Frontal, "Frontal", Cuad, Var, Multi, Alta, Alta, 2, F(Frontal)),
                new(Extension, "Extension", Cuad, Var, Aisl, Mod, null, 3, F(Extension)),
                new(CurlSentado, "CurlSentado", Isq, Ref, Aisl, null, null, 1, F(CurlSentado)),
                new(CurlAcostado, "CurlAcostado", Isq, Var, Aisl, null, null, 1, F(CurlAcostado)),
                new(PesoMuerto, "PesoMuerto", Isq, Var, Multi, null, Mod, 2, F(PesoMuerto)),
                new(TalonesPie, "TalonesPie", Pan, Var, Aisl, null, null, 1, F(TalonesPie)),
                new(TalonesSentado, "TalonesSentado", Pan, Var, Aisl, null, null, 2, F(TalonesSentado)),
            };
        }

        public static PerfilPlanificacion Perfil(int generacion = 1, int edad = 30, NivelExperiencia nivel = NivelExperiencia.Intermedio,
            NivelLimitacion rodilla = Sin, NivelLimitacion lumbar = Sin, bool alerta = false,
            Dictionary<Guid, HistorialEjercicio>? historial = null) =>
            new(Guid.Parse("11111111-1111-1111-1111-111111111111"), edad, nivel, rodilla, lumbar, generacion, alerta,
                historial ?? new Dictionary<Guid, HistorialEjercicio>());

        private static ResultadoPlanificacion Planificar(PerfilPlanificacion p, List<CandidatoProtocolo>? c = null,
            ProtocoloPiernasConfig? config = null) =>
            PlanificadorPiernas.Planificar(p, c ?? Catalogo(), config ?? new ProtocoloPiernasConfig(), new ReglasSeguridadConfig());

        /// <summary>Historial con buena adherencia (3 x 10 dentro de 8-12) y la misma carga.</summary>
        private static HistorialEjercicio BuenHistorial(Guid ejercicio, decimal carga = 100m, int reps = 10, int rir = 2) =>
            new(ejercicio, Guid.NewGuid(), new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc),
                Enumerable.Range(1, 3).Select(_ => new SerieEvaluada(carga, reps, rir, false)).ToList(),
                new ObjetivoEjercicio(3, 8, 12, 2, TipoMedida.Repeticiones));

        private static Dictionary<Guid, HistorialEjercicio> HistorialDeTodos() =>
            Catalogo().ToDictionary(c => c.EjercicioId, c => BuenHistorial(c.EjercicioId));

        // ---- Determinismo y composición ----

        [Fact]
        public void Dos_corridas_con_las_mismas_entradas_dan_el_mismo_resultado_incluido_el_orden()
        {
            var historial = HistorialDeTodos();
            var a = JsonSerializer.Serialize(Planificar(Perfil(generacion: 3, historial: historial)));
            var b = JsonSerializer.Serialize(Planificar(Perfil(generacion: 3, historial: historial)));
            Assert.Equal(a, b);
        }

        [Fact]
        public void El_orden_de_los_candidatos_de_entrada_no_cambia_el_resultado()
        {
            var normal = Planificar(Perfil(generacion: 2));
            var invertido = Planificar(Perfil(generacion: 2), Enumerable.Reverse(Catalogo()).ToList());
            Assert.Equal(normal.Lineas.Select(l => l.EjercicioId), invertido.Lineas.Select(l => l.EjercicioId));
        }

        [Fact]
        public void Sin_historial_ni_alertas_arma_seis_posiciones_con_16_series_en_el_orden_de_la_composicion()
        {
            var r = Planificar(Perfil());
            Assert.Equal(new[] { Prensa, Hack, Trasera, CurlSentado, CurlAcostado, TalonesPie }, r.Lineas.Select(l => l.EjercicioId));
            Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, r.Lineas.Select(l => l.Orden));
            Assert.Equal(new[] { 3, 3, 3, 2, 2, 3 }, r.Lineas.Select(l => l.Series));
            Assert.Equal(16, r.SeriesTotales);
            Assert.Empty(r.Omitidas);
            Assert.All(r.Lineas, l => Assert.Equal(2, l.Rir));
            Assert.Equal((8, 12), (r.Lineas[0].RepeticionesMinimas, r.Lineas[0].RepeticionesMaximas));
            Assert.Equal((10, 15), (r.Lineas[3].RepeticionesMinimas, r.Lineas[3].RepeticionesMaximas));
            Assert.Equal((15, 20), (r.Lineas[5].RepeticionesMinimas, r.Lineas[5].RepeticionesMaximas));
            Assert.Equal(new[] { 150, 150, 150, 75, 75, 75 }, r.Lineas.Select(l => l.DescansoSegundos));
            Assert.All(r.Lineas, l => Assert.Equal(3, l.TempoExcentricoSeg));
            Assert.Equal(new[] { Ref, Ref, Var, Ref, Var, Var }, r.Lineas.Select(l => l.Rol));
        }

        // ---- Rotación de variables ----

        [Theory]
        [InlineData(1, 0)]
        [InlineData(2, 1)]
        [InlineData(3, 2)]
        [InlineData(4, 0)]
        [InlineData(5, 1)]
        [InlineData(6, 2)]
        public void Con_tres_candidatos_la_variable_de_cuadriceps_rota_por_generacion(int generacion, int indice)
        {
            var r = Planificar(Perfil(generacion: generacion));
            var linea = r.Lineas.Single(l => l.Grupo == Cuad && l.Rol == Var);
            Assert.Equal(new[] { Trasera, Frontal, Extension }[indice], linea.EjercicioId);
            Assert.Equal(indice, linea.Motivo.IndiceRotacion);
            Assert.Equal(3, linea.Motivo.CantidadCandidatos);
            Assert.Equal(new[] { Trasera, Frontal, Extension }, linea.Motivo.Candidatos);
        }

        [Theory]
        [InlineData(1, 0)]
        [InlineData(2, 1)]
        [InlineData(3, 0)]
        [InlineData(4, 1)]
        [InlineData(5, 0)]
        [InlineData(6, 1)]
        public void Con_dos_candidatos_las_variables_de_isquiotibiales_y_pantorrillas_alternan(int generacion, int indice)
        {
            var r = Planificar(Perfil(generacion: generacion));
            Assert.Equal(new[] { CurlAcostado, PesoMuerto }[indice], r.Lineas.Single(l => l.Grupo == Isq && l.Rol == Var).EjercicioId);
            Assert.Equal(new[] { TalonesPie, TalonesSentado }[indice], r.Lineas.Single(l => l.Grupo == Pan).EjercicioId);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        public void Con_un_solo_candidato_la_variable_no_cambia(int generacion)
        {
            var catalogo = Catalogo(id => id != Frontal && id != Extension);
            var linea = Planificar(Perfil(generacion: generacion), catalogo).Lineas.Single(l => l.Grupo == Cuad && l.Rol == Var);
            Assert.Equal(Trasera, linea.EjercicioId);
            Assert.Equal((0, 1), (linea.Motivo.IndiceRotacion, linea.Motivo.CantidadCandidatos));
        }

        [Fact]
        public void El_numero_de_generacion_empieza_en_1()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Planificar(Perfil(generacion: 0)));
        }

        // ---- Exclusión por limitación articular ----

        [Theory]
        [InlineData(Sin, Sin)]
        [InlineData(Sin, Mod)]
        [InlineData(Sin, Alta)]
        [InlineData(Mod, Sin)]
        [InlineData(Mod, Mod)]
        [InlineData(Mod, Alta)]
        [InlineData(Alta, Sin)]
        [InlineData(Alta, Mod)]
        [InlineData(Alta, Alta)]
        public void Ninguna_combinacion_de_limitaciones_prescribe_un_ejercicio_excluido(NivelLimitacion rodilla, NivelLimitacion lumbar)
        {
            var catalogo = Catalogo();
            bool Excluido(CandidatoProtocolo c) =>
                (c.RodillaExcluirDesde is { } r && rodilla >= r) || (c.LumbarExcluirDesde is { } l && lumbar >= l);
            var cuadElegibles = catalogo.Where(c => c.Grupo == Cuad && !Excluido(c)).ToList();

            if (cuadElegibles.Count == 0)
            {
                // Rodilla Alta excluye todo cuádriceps: no hay rutina y el mensaje indica el grupo.
                var ex = Assert.Throws<ReglaNegocioException>(() => Planificar(Perfil(rodilla: rodilla, lumbar: lumbar), catalogo));
                Assert.Contains("Cuádriceps", ex.Message);
                Assert.Equal(Alta, rodilla);
                return;
            }

            var r = Planificar(Perfil(rodilla: rodilla, lumbar: lumbar), catalogo);
            Assert.All(r.Lineas, l => Assert.False(Excluido(catalogo.Single(c => c.EjercicioId == l.EjercicioId))));
            var registrados = r.Lineas.SelectMany(l => l.Motivo.ExcluidosPorLimitacion).Concat(r.Omitidas.SelectMany(o => o.ExcluidosPorLimitacion))
                .Select(x => x.EjercicioId).ToHashSet();
            Assert.Equal(catalogo.Where(Excluido).Select(c => c.EjercicioId).ToHashSet(), registrados);
        }

        [Fact]
        public void Rodilla_moderada_excluye_la_extension_y_lo_registra_en_el_motivo()
        {
            var r = Planificar(Perfil(generacion: 3, rodilla: Mod));
            var variable = r.Lineas.Single(l => l.Grupo == Cuad && l.Rol == Var);
            Assert.Equal(new[] { Trasera, Frontal }, variable.Motivo.Candidatos);
            Assert.Equal(Trasera, variable.EjercicioId);                       // (3 - 1) mod 2 = 0
            var exclusion = Assert.Single(variable.Motivo.ExcluidosPorLimitacion);
            Assert.Equal((Extension, "rodilla", "Moderada", "Moderada"),
                (exclusion.EjercicioId, exclusion.Articulacion, exclusion.NivelSocio, exclusion.ExcluirDesde));
        }

        [Fact]
        public void Lumbar_moderada_excluye_sentadilla_trasera_y_peso_muerto_rumano()
        {
            var r = Planificar(Perfil(lumbar: Mod));
            Assert.Equal(new[] { Frontal, Extension }, r.Lineas.Single(l => l.Grupo == Cuad && l.Rol == Var).Motivo.Candidatos);
            Assert.Equal(new[] { CurlAcostado }, r.Lineas.Single(l => l.Grupo == Isq && l.Rol == Var).Motivo.Candidatos);
            Assert.DoesNotContain(r.Lineas, l => l.EjercicioId == Trasera || l.EjercicioId == PesoMuerto);
        }

        [Fact]
        public void Lumbar_alta_omite_la_prensa_sin_sustituirla_y_deja_cinco_ejercicios()
        {
            var r = Planificar(Perfil(lumbar: Alta));
            Assert.Equal(new[] { Hack, Extension, CurlSentado, CurlAcostado, TalonesPie }, r.Lineas.Select(l => l.EjercicioId));
            var omitida = Assert.Single(r.Omitidas);
            Assert.Equal((1, "Cuadriceps", "Referencia"), (omitida.Posicion, omitida.Grupo, omitida.Rol));
            Assert.Contains(omitida.ExcluidosPorLimitacion, x => x.EjercicioId == Prensa && x.Articulacion == "lumbar");
        }

        // ---- Equipamiento y referencias ----

        [Fact]
        public void Sin_equipamiento_para_la_hack_la_referencia_se_omite_y_no_se_sustituye()
        {
            var r = Planificar(Perfil(), Catalogo(id => id != Hack));
            Assert.Equal(new[] { Prensa, Trasera, CurlSentado, CurlAcostado, TalonesPie }, r.Lineas.Select(l => l.EjercicioId));
            var omitida = Assert.Single(r.Omitidas);
            Assert.Equal(2, omitida.Posicion);
            Assert.Equal(new[] { Hack }, omitida.ExcluidosPorEquipamiento);
            Assert.Contains("no se sustituye", omitida.Motivo);
            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, r.Lineas.Select(l => l.Orden));
        }

        [Fact]
        public void La_exclusion_por_equipamiento_de_una_variable_queda_en_el_motivo()
        {
            var r = Planificar(Perfil(), Catalogo(id => id != Trasera));
            var variable = r.Lineas.Single(l => l.Grupo == Cuad && l.Rol == Var);
            Assert.Equal(Frontal, variable.EjercicioId);
            Assert.Equal(new[] { Trasera }, variable.Motivo.ExcluidosPorEquipamiento);
        }

        [Fact]
        public void Si_falta_un_grupo_en_el_catalogo_falla_indicando_el_grupo()
        {
            var sinPantorrillas = Catalogo().Where(c => c.Grupo != Pan).ToList();
            var ex = Assert.Throws<ReglaNegocioException>(() => Planificar(Perfil(), sinPantorrillas));
            Assert.Contains("Pantorrillas", ex.Message);
            Assert.Contains("catálogo del protocolo", ex.Message);
        }

        [Fact]
        public void Si_ningun_ejercicio_de_un_grupo_es_factible_falla_indicando_el_grupo()
        {
            var ex = Assert.Throws<ReglaNegocioException>(() => Planificar(Perfil(), Catalogo(id => id != TalonesPie && id != TalonesSentado)));
            Assert.Contains("Pantorrillas", ex.Message);
            Assert.Contains("equipamiento", ex.Message);
        }

        // ---- Ajustes por edad, alerta y adherencia ----

        [Theory]
        [InlineData(39, Sin, 3, 150)]
        [InlineData(40, Sin, 4, 180)]
        [InlineData(25, Mod, 4, 180)]
        public void Con_40_anos_o_una_limitacion_moderada_el_tempo_es_4_y_el_descanso_multiarticular_180(int edad, NivelLimitacion lumbar,
            int tempo, int descansoMulti)
        {
            var r = Planificar(Perfil(edad: edad, lumbar: lumbar));
            Assert.All(r.Lineas, l => Assert.Equal(tempo, l.TempoExcentricoSeg));
            Assert.All(r.Lineas.Where(l => l.Clase == Multi), l => Assert.Equal(descansoMulti, l.DescansoSegundos));
            Assert.All(r.Lineas.Where(l => l.Clase == Aisl), l => Assert.Equal(75, l.DescansoSegundos));
        }

        [Fact]
        public void Con_alerta_vigente_las_series_bajan_30_por_ciento_con_piso_2_y_queda_marcado()
        {
            var r = Planificar(Perfil(alerta: true, historial: HistorialDeTodos()));
            Assert.All(r.Lineas, l => Assert.Equal(2, l.Series));          // 3 -> 2 y 2 -> 2 (piso); sin serie extra
            Assert.True(r.ReduccionPorAlerta);
            Assert.All(r.Lineas, l => Assert.True(l.Motivo.ReduccionPorAlerta));
            Assert.All(r.Lineas, l => Assert.Contains(PlanificadorPiernas.ReglaReduccionAlerta, l.Motivo.ReglasAplicadas));
            Assert.All(r.Lineas, l => Assert.Equal(2, l.Rir));             // con alerta no baja a RIR 1
        }

        [Fact]
        public void La_reduccion_por_alerta_redondea_4_series_a_3()
        {
            var config = new ProtocoloPiernasConfig { SeriesBaseMultiarticular = 4 };
            var r = Planificar(Perfil(alerta: true), config: config);
            Assert.Equal(3, r.Lineas.First(l => l.EjercicioId == Prensa).Series);   // round(4 x 0,7 = 2,8) = 3
        }

        [Fact]
        public void La_reduccion_nunca_sube_las_series_aunque_el_piso_sea_mayor()
        {
            var config = new ProtocoloPiernasConfig { SeriesBaseAislamiento = 1 };
            var r = Planificar(Perfil(alerta: true), config: config);
            Assert.Equal(1, r.Lineas.Single(l => l.EjercicioId == CurlSentado).Series);
        }

        [Fact]
        public void Con_buena_adherencia_un_intermedio_baja_a_RIR_1_y_nunca_a_0()
        {
            var r = Planificar(Perfil(historial: HistorialDeTodos()));
            Assert.All(r.Lineas, l => Assert.Equal(1, l.Rir));

            var config = new ProtocoloPiernasConfig { RirObjetivo = 0, RirMinimo = 0 };
            var forzado = Planificar(Perfil(historial: HistorialDeTodos()), config: config);
            Assert.All(forzado.Lineas, l => Assert.True(l.Rir >= 1));
        }

        [Fact]
        public void Un_principiante_mantiene_RIR_2_y_no_suma_series_aunque_tenga_buena_adherencia()
        {
            var r = Planificar(Perfil(nivel: NivelExperiencia.Principiante, historial: HistorialDeTodos()));
            Assert.All(r.Lineas, l => Assert.True(l.Rir >= 2));
            Assert.Equal(16, r.SeriesTotales);
        }

        [Fact]
        public void Con_adherencia_menor_al_80_por_ciento_no_hay_ajustes()
        {
            var historial = new Dictionary<Guid, HistorialEjercicio>
            {
                [Prensa] = new(Prensa, Guid.NewGuid(), DateTime.UtcNow, new List<SerieEvaluada>
                {
                    new(100, 10, 2, false), new(100, 10, 2, false), new(100, 6, 2, false), new(100, 6, 2, false), new(60, 15, null, true),
                }, new ObjetivoEjercicio(3, 8, 12, 2, TipoMedida.Repeticiones)),
            };
            var linea = Planificar(Perfil(historial: historial)).Lineas.Single(l => l.EjercicioId == Prensa);
            Assert.Equal(50m, linea.Motivo.AdherenciaUltimoRegistro);   // 2 de 4 series de trabajo (el calentamiento no cuenta)
            Assert.Equal((3, 2), (linea.Series, linea.Rir));
        }

        [Fact]
        public void La_serie_extra_respeta_el_tope_de_series_por_sesion_en_el_orden_de_la_composicion()
        {
            var r = Planificar(Perfil(historial: HistorialDeTodos()));
            // Base 16; tope 18: solo las dos primeras posiciones con margen suman una serie.
            Assert.Equal(new[] { 4, 4, 3, 2, 2, 3 }, r.Lineas.Select(l => l.Series));
            Assert.Equal(18, r.SeriesTotales);
            Assert.Contains(PlanificadorPiernas.ReglaTopeSesion, r.Lineas[2].Motivo.ReglasAplicadas);
            Assert.Contains(PlanificadorPiernas.ReglaSerieExtra, r.Lineas[0].Motivo.ReglasAplicadas);
        }

        [Fact]
        public void Una_configuracion_que_supera_el_tope_de_series_falla()
        {
            var config = new ProtocoloPiernasConfig { SeriesTotalesMaxSesion = 10 };
            var ex = Assert.Throws<ReglaNegocioException>(() => Planificar(Perfil(), config: config));
            Assert.Contains("máximo por sesión", ex.Message);
        }

        // ---- Carga sugerida (informativa) ----

        [Fact]
        public void Carga_sugerida_por_R_PROG_1_cuando_el_ultimo_registro_cumple_la_subida()
        {
            var historial = new Dictionary<Guid, HistorialEjercicio> { [Prensa] = BuenHistorial(Prensa, carga: 100m, reps: 12, rir: 2) };
            var linea = Planificar(Perfil(historial: historial)).Lineas.Single(l => l.EjercicioId == Prensa);
            Assert.Equal(102.5m, linea.CargaSugeridaKg);   // +2,5 %
            Assert.Equal(PlanificadorPiernas.FuenteProgresion, linea.Motivo.FuenteCargaSugerida);
        }

        [Fact]
        public void Carga_sugerida_igual_a_la_carga_actual_cuando_R_PROG_1_no_sugiere()
        {
            var historial = new Dictionary<Guid, HistorialEjercicio> { [Prensa] = BuenHistorial(Prensa, carga: 80m, reps: 10) };
            var linea = Planificar(Perfil(historial: historial)).Lineas.Single(l => l.EjercicioId == Prensa);
            Assert.Equal(80m, linea.CargaSugeridaKg);
            Assert.Equal(PlanificadorPiernas.FuenteCargaActual, linea.Motivo.FuenteCargaSugerida);
        }

        [Fact]
        public void Sin_historial_no_hay_carga_sugerida()
        {
            var linea = Planificar(Perfil()).Lineas.Single(l => l.EjercicioId == Prensa);
            Assert.Null(linea.CargaSugeridaKg);
            Assert.Equal(PlanificadorPiernas.FuenteSinHistorial, linea.Motivo.FuenteCargaSugerida);
        }

        [Fact]
        public void Con_cargas_distintas_y_sin_sugerencia_no_hay_carga_sugerida()
        {
            var historial = new Dictionary<Guid, HistorialEjercicio>
            {
                [Prensa] = new(Prensa, Guid.NewGuid(), DateTime.UtcNow,
                    new List<SerieEvaluada> { new(80, 10, 2, false), new(90, 10, 2, false) }, null),
            };
            var linea = Planificar(Perfil(historial: historial)).Lineas.Single(l => l.EjercicioId == Prensa);
            Assert.Null(linea.CargaSugeridaKg);
            Assert.Equal(PlanificadorPiernas.FuenteSinCargaUniforme, linea.Motivo.FuenteCargaSugerida);
        }

        [Fact]
        public void Un_ejercicio_en_segundos_nunca_tiene_carga_sugerida()
        {
            var catalogo = Catalogo().Select(c => c.EjercicioId == TalonesPie ? c with { TipoMedida = TipoMedida.Segundos } : c).ToList();
            var historial = new Dictionary<Guid, HistorialEjercicio> { [TalonesPie] = BuenHistorial(TalonesPie, carga: 40m) };
            var linea = Planificar(Perfil(historial: historial), catalogo).Lineas.Single(l => l.EjercicioId == TalonesPie);
            Assert.Null(linea.CargaSugeridaKg);
            Assert.Equal(PlanificadorPiernas.FuenteSegundos, linea.Motivo.FuenteCargaSugerida);
        }

        [Fact]
        public void La_adherencia_al_rango_ignora_el_calentamiento()
        {
            var series = new[] { new SerieEvaluada(50, 20, null, true), new SerieEvaluada(100, 9, 2, false), new SerieEvaluada(100, 13, 2, false) };
            Assert.Equal(50m, Adherencia.AlRango(series, 8, 12));
            Assert.Null(Adherencia.AlRango(new[] { new SerieEvaluada(50, 20, null, true) }, 8, 12));
        }
    }
}
