using GymBro.Domain.Enums;
using GymBro.Domain.Rules;
using Xunit;

namespace GymBro.Tests
{
    /// <summary>Etapa 10a: reglas puras de GymBro.Domain.Rules con los umbrales por defecto. Casos en el umbral, antes y después.</summary>
    public class ReglasSeguridadTests
    {
        private static readonly ReglasSeguridadConfig Cfg = new();

        private static SerieEvaluada S(int reps, int? rir, decimal kg = 40m, bool calentamiento = false) => new(kg, reps, rir, calentamiento);

        // ---------------- Mediana ----------------

        [Fact]
        public void Mediana_impar_par_y_vacia()
        {
            Assert.Equal(2m, Estadistica.Mediana(new[] { 3m, 1m, 2m }));
            Assert.Equal(1.5m, Estadistica.Mediana(new[] { 2m, 1m }));
            Assert.Null(Estadistica.Mediana(Array.Empty<decimal>()));
        }

        // ---------------- R-RIR-1 ----------------

        [Theory]
        [InlineData(new[] { 1, 1, 1 }, 3, true)]   // diferencia 2: justo en el umbral
        [InlineData(new[] { 2, 2, 2 }, 3, false)]  // diferencia 1: un paso antes
        [InlineData(new[] { 0, 0, 0 }, 3, true)]   // diferencia 3: un paso después
        [InlineData(new[] { 0, 1, 3 }, 3, true)]   // usa la mediana (1), no el mínimo ni el promedio
        [InlineData(new[] { 0, 3, 3 }, 3, false)]  // mediana 3 aunque haya un 0
        public void RIR_alerta_segun_la_mediana(int[] rirs, int objetivo, bool alerta)
        {
            var r = ReglaRir.Evaluar(rirs.Select(x => S(10, x)), objetivo, Cfg.Rir);
            Assert.NotNull(r);
            Assert.Equal(alerta, r!.Alerta);
        }

        [Fact]
        public void RIR_con_cantidad_par_usa_el_promedio_central()
        {
            var r = ReglaRir.Evaluar(new[] { S(10, 1), S(10, 2) }, 3, Cfg.Rir)!;
            Assert.Equal((1.5m, 1.5m, false), (r.MedianaRealizada, r.Diferencia, r.Alerta));
        }

        [Fact]
        public void RIR_exige_al_menos_dos_series_con_dato_y_excluye_calentamiento_y_nulos()
        {
            Assert.Null(ReglaRir.Evaluar(new[] { S(10, 0) }, 3, Cfg.Rir));                                    // una sola serie
            Assert.Null(ReglaRir.Evaluar(new[] { S(10, 0), S(10, null), S(10, null) }, 3, Cfg.Rir));          // RIR nulo no cuenta
            Assert.Null(ReglaRir.Evaluar(new[] { S(10, 0), S(10, 0, calentamiento: true) }, 3, Cfg.Rir));     // calentamiento excluido
            Assert.Null(ReglaRir.Evaluar(new[] { S(10, 0), S(10, 0) }, null, Cfg.Rir));                       // sin objetivo
            var r = ReglaRir.Evaluar(new[] { S(10, 5, calentamiento: true), S(10, 1), S(10, 1) }, 3, Cfg.Rir)!;
            Assert.Equal((2, true), (r.SeriesConDato, r.Alerta));
        }

        // ---------------- R-PROG-1 ----------------

        private static readonly ObjetivoEjercicio Obj = new(3, 8, 12, 2, TipoMedida.Repeticiones);

        [Fact]
        public void PROG_sube_2_5_por_ciento_redondeado()
        {
            var s = ReglaProgresion.Evaluar(new[] { S(12, 2), S(12, 2), S(12, 3) }, Obj, false, Cfg.Progresion)!;
            Assert.Equal((DireccionCarga.Subir, 40m, 41m, 1m), (s.Direccion, s.CargaActualKg, s.CargaSugeridaKg, s.CambioKg));
            // 41 kg x 1,025 = 42,025 -> 42,0
            Assert.Equal(42m, ReglaProgresion.Evaluar(new[] { S(12, 2, 41), S(12, 2, 41), S(12, 2, 41) }, Obj, false, Cfg.Progresion)!.CargaSugeridaKg);
        }

        [Theory]
        [InlineData(20.25, 20.5)]   // punto medio: hacia arriba
        [InlineData(20.75, 21.0)]   // punto medio: hacia arriba
        [InlineData(20.24, 20.0)]
        [InlineData(20.26, 20.5)]
        [InlineData(4.75, 5.0)]
        public void Redondeo_a_medio_kilo_con_puntos_medios_hacia_arriba(decimal valor, decimal esperado) =>
            Assert.Equal(esperado, ReglaProgresion.Redondear(valor, 0.5m));

        [Fact]
        public void PROG_mantiene_si_no_todas_llegan_al_maximo_o_el_RIR_es_menor_al_objetivo()
        {
            Assert.Null(ReglaProgresion.Evaluar(new[] { S(12, 2), S(12, 2), S(11, 2) }, Obj, false, Cfg.Progresion));
            Assert.Null(ReglaProgresion.Evaluar(new[] { S(12, 1), S(12, 1), S(12, 2) }, Obj, false, Cfg.Progresion)); // mediana 1 < 2
        }

        [Fact]
        public void PROG_subir_exige_al_menos_las_series_objetivo()
        {
            Assert.Null(ReglaProgresion.Evaluar(new[] { S(12, 2), S(12, 2) }, Obj, false, Cfg.Progresion)); // 2 de 3
        }

        [Fact]
        public void PROG_baja_si_al_menos_la_mitad_no_llega_al_minimo()
        {
            var s = ReglaProgresion.Evaluar(new[] { S(7, 2), S(9, 2) }, Obj, false, Cfg.Progresion)!;   // 1 de 2: la mitad
            Assert.Equal((DireccionCarga.Bajar, 38m, -2m), (s.Direccion, s.CargaSugeridaKg, s.CambioKg));
            Assert.Null(ReglaProgresion.Evaluar(new[] { S(7, 2), S(9, 2), S(10, 2) }, Obj, false, Cfg.Progresion)); // 1 de 3
        }

        [Fact]
        public void PROG_cambio_minimo_medio_kilo_y_tope_diez_por_ciento()
        {
            // 10 kg: 2,5 % = 0,25 -> 10,5 por redondeo (0,5 kg, 5 %).
            Assert.Equal(10.5m, ReglaProgresion.Evaluar(new[] { S(12, 2, 10), S(12, 2, 10), S(12, 2, 10) }, Obj, false, Cfg.Progresion)!.CargaSugeridaKg);
            // 6 kg: 6,15 -> 6,0 -> cambio mínimo 0,5 -> 6,5 (8,3 %, dentro del tope).
            Assert.Equal(6.5m, ReglaProgresion.Evaluar(new[] { S(12, 2, 6), S(12, 2, 6), S(12, 2, 6) }, Obj, false, Cfg.Progresion)!.CargaSugeridaKg);
            // 5 kg: +0,5 es exactamente el 10 %: el tope lo admite.
            Assert.Equal(5.5m, ReglaProgresion.Evaluar(new[] { S(12, 2, 5), S(12, 2, 5), S(12, 2, 5) }, Obj, false, Cfg.Progresion)!.CargaSugeridaKg);
            Assert.Equal(4.5m, ReglaProgresion.Evaluar(new[] { S(5, 2, 5), S(5, 2, 5) }, Obj, false, Cfg.Progresion)!.CargaSugeridaKg);
            // 4,5 kg: +0,5 es el 11,1 %: el tope gana y no hay sugerencia, ni para subir ni para bajar.
            Assert.Null(ReglaProgresion.Evaluar(new[] { S(12, 2, 4.5m), S(12, 2, 4.5m), S(12, 2, 4.5m) }, Obj, false, Cfg.Progresion));
            Assert.Null(ReglaProgresion.Evaluar(new[] { S(5, 2, 4.5m), S(5, 2, 4.5m) }, Obj, false, Cfg.Progresion));
            // Carga que no es múltiplo de 0,5: el cambio mínimo deja la sugerencia en un múltiplo (5,7 no; 6,0 sería 15 %: nada).
            Assert.Null(ReglaProgresion.Evaluar(new[] { S(12, 2, 5.2m), S(12, 2, 5.2m), S(12, 2, 5.2m) }, Obj, false, Cfg.Progresion));
        }

        [Fact]
        public void PROG_sin_sugerencia_en_segundos_peso_corporal_con_RIR1_o_datos_faltantes()
        {
            var tres = new[] { S(12, 2), S(12, 2), S(12, 2) };
            Assert.Null(ReglaProgresion.Evaluar(tres, Obj with { TipoMedida = TipoMedida.Segundos }, false, Cfg.Progresion));
            Assert.Null(ReglaProgresion.Evaluar(new[] { S(12, 2, 0), S(12, 2, 0), S(12, 2, 0) }, Obj, false, Cfg.Progresion));
            Assert.Null(ReglaProgresion.Evaluar(tres, Obj, true, Cfg.Progresion));                                        // hay R-RIR-1
            Assert.Null(ReglaProgresion.Evaluar(new[] { S(12, 2), S(12, null), S(12, 2) }, Obj, false, Cfg.Progresion));  // RIR faltante
            Assert.Null(ReglaProgresion.Evaluar(tres, Obj with { RirObjetivo = null }, false, Cfg.Progresion));
            Assert.Null(ReglaProgresion.Evaluar(new[] { S(12, 2, 40), S(12, 2, 42.5m), S(12, 2, 40) }, Obj, false, Cfg.Progresion)); // cargas distintas (P1)
        }

        [Fact]
        public void PROG_ignora_el_calentamiento()
        {
            var s = ReglaProgresion.Evaluar(new[] { S(15, null, 20, true), S(12, 2), S(12, 2), S(12, 2) }, Obj, false, Cfg.Progresion)!;
            Assert.Equal((40m, 41m), (s.CargaActualKg, s.CargaSugeridaKg));
        }

        // ---------------- R-FC-1 ----------------

        private static short?[] Base(params short?[] v) => v;

        [Fact]
        public void FC_con_cuatro_registros_no_evalua_y_con_cinco_si()
        {
            Assert.Null(ReglaFcReposo.Evaluar(70, Base(60, 60, 60, 60), Cfg.FcReposo));
            Assert.NotNull(ReglaFcReposo.Evaluar(70, Base(60, 60, 60, 60, 60), Cfg.FcReposo));
            Assert.Null(ReglaFcReposo.Evaluar(null, Base(60, 60, 60, 60, 60), Cfg.FcReposo));     // sin dato del día
        }

        [Fact]
        public void FC_la_mediana_ignora_los_dias_sin_dato()
        {
            var r = ReglaFcReposo.Evaluar(66, Base(60, null, 60, null, 60, 60, null, 60), Cfg.FcReposo)!;
            Assert.Equal((60m, 5, true), (r.LineaBase, r.RegistrosLineaBase, r.Alerta));
        }

        [Theory]
        [InlineData(55, 50, true)]    // +5 lpm y +10 %: justo en los dos umbrales
        [InlineData(66, 60, true)]    // +6 lpm y +10 %
        [InlineData(65, 60, false)]   // +5 lpm pero +8,3 %: cumple solo el de lpm
        [InlineData(44, 40, false)]   // +10 % pero +4 lpm: cumple solo el de porcentaje
        [InlineData(54, 50, false)]   // +4 lpm y +8 %: ninguno
        public void FC_exige_las_dos_condiciones(short valor, short lineaBase, bool alerta) =>
            Assert.Equal(alerta, ReglaFcReposo.Evaluar(valor, Enumerable.Repeat((short?)lineaBase, 5), Cfg.FcReposo)!.Alerta);

        [Fact]
        public void FC_umbral_de_porcentaje_y_de_lpm_por_separado()
        {
            // Línea base 40: +5 lpm es +12,5 % -> alerta. +4 lpm es +10 % pero no llega a 5 lpm -> sin alerta.
            Assert.True(ReglaFcReposo.Evaluar(45, Enumerable.Repeat((short?)40, 5), Cfg.FcReposo)!.Alerta);
            Assert.False(ReglaFcReposo.Evaluar(44, Enumerable.Repeat((short?)40, 5), Cfg.FcReposo)!.Alerta);
            // Línea base 80: +7 lpm es +8,75 % -> sin alerta; +8 lpm es +10 % -> alerta.
            Assert.False(ReglaFcReposo.Evaluar(87, Enumerable.Repeat((short?)80, 5), Cfg.FcReposo)!.Alerta);
            Assert.True(ReglaFcReposo.Evaluar(88, Enumerable.Repeat((short?)80, 5), Cfg.FcReposo)!.Alerta);
            Assert.Equal(10m, ReglaFcReposo.Evaluar(88, Enumerable.Repeat((short?)80, 5), Cfg.FcReposo)!.VariacionPorcentaje);
        }
    }
}
