using GymBro.Domain.Enums;
using GymBro.Domain.Rules;
using Xunit;

namespace GymBro.Tests
{
    /// <summary>Etapa 10a: R-LIM (límites duros) con los valores por defecto. Cada límite, en el borde y fuera de él.</summary>
    public class ValidadorPrescripcionTests
    {
        private static readonly LimitesPrescripcion L = new();

        private static LineaPrescripcion Linea(int orden = 1, int series = 3, int min = 8, int max = 12, TipoMedida medida = TipoMedida.Repeticiones,
            int? rir = 2, int descanso = 90, Guid? ejercicio = null) =>
            new(ejercicio ?? Guid.NewGuid(), orden, $"E{orden}", series, min, max, medida, rir, descanso);

        private static IReadOnlyList<string> Codigos(IEnumerable<LineaPrescripcion> lineas, NivelExperiencia? nivel = NivelExperiencia.Intermedio) =>
            ValidadorPrescripcion.Validar(lineas.ToList(), nivel, L).Select(v => v.Codigo).ToList();

        [Fact]
        public void Una_rutina_dentro_de_los_limites_no_tiene_violaciones() =>
            Assert.Empty(Codigos(new[] { Linea(1), Linea(2) }, NivelExperiencia.Principiante));

        [Theory]
        [InlineData(1, false)]
        [InlineData(6, false)]
        [InlineData(0, true)]
        [InlineData(7, true)]
        public void Series_por_ejercicio_1_a_6(int series, bool viola) =>
            Assert.Equal(viola, Codigos(new[] { Linea(series: series) }).Contains(ValidadorPrescripcion.Series));

        [Theory]
        [InlineData(1, 30, false)]
        [InlineData(0, 10, true)]
        [InlineData(8, 31, true)]
        public void Repeticiones_1_a_30(int min, int max, bool viola) =>
            Assert.Equal(viola, Codigos(new[] { Linea(min: min, max: max) }).Contains(ValidadorPrescripcion.Repeticiones));

        [Theory]
        [InlineData(5, 180, false)]
        [InlineData(4, 30, true)]
        [InlineData(20, 181, true)]
        public void Segundos_5_a_180(int min, int max, bool viola)
        {
            var c = Codigos(new[] { Linea(min: min, max: max, medida: TipoMedida.Segundos) });
            Assert.Equal(viola, c.Contains(ValidadorPrescripcion.Segundos));
            Assert.DoesNotContain(ValidadorPrescripcion.Repeticiones, c);   // en segundos no aplica el rango de repeticiones
        }

        [Theory]
        [InlineData(30, false)]
        [InlineData(600, false)]
        [InlineData(29, true)]
        [InlineData(601, true)]
        public void Descanso_30_a_600(int descanso, bool viola) =>
            Assert.Equal(viola, Codigos(new[] { Linea(descanso: descanso) }).Contains(ValidadorPrescripcion.Descanso));

        [Theory]
        [InlineData(0, false)]
        [InlineData(5, false)]
        [InlineData(-1, true)]
        [InlineData(6, true)]
        public void RIR_0_a_5(int rir, bool viola) =>
            Assert.Equal(viola, Codigos(new[] { Linea(rir: rir) }).Contains(ValidadorPrescripcion.Rir));

        [Theory]
        [InlineData(NivelExperiencia.Principiante, 1, true)]
        [InlineData(NivelExperiencia.Principiante, 2, false)]
        [InlineData(NivelExperiencia.Intermedio, 1, false)]
        [InlineData(NivelExperiencia.Avanzado, 0, false)]
        public void RIR_minimo_2_para_principiante(NivelExperiencia nivel, int rir, bool viola) =>
            Assert.Equal(viola, Codigos(new[] { Linea(rir: rir) }, nivel).Contains(ValidadorPrescripcion.RirPrincipiante));

        [Fact]
        public void RIR_nulo_no_se_evalua() =>
            Assert.Empty(Codigos(new[] { Linea(rir: null) }, NivelExperiencia.Principiante));

        [Theory]
        [InlineData(0, true)]
        [InlineData(1, false)]
        [InlineData(12, false)]
        [InlineData(13, true)]
        public void Cantidad_de_ejercicios_1_a_12(int cantidad, bool viola) =>
            Assert.Equal(viola, Codigos(Enumerable.Range(1, cantidad).Select(i => Linea(i, series: 1)))
                .Contains(ValidadorPrescripcion.CantidadEjercicios));

        [Fact]
        public void Hasta_30_series_en_total()
        {
            Assert.DoesNotContain(ValidadorPrescripcion.SeriesTotales, Codigos(Enumerable.Range(1, 5).Select(i => Linea(i, series: 6))));
            Assert.Contains(ValidadorPrescripcion.SeriesTotales, Codigos(Enumerable.Range(1, 6).Select(i => Linea(i, series: i == 6 ? 1 : 6))));
        }

        [Fact]
        public void Sin_ejercicio_repetido()
        {
            var id = Guid.NewGuid();
            Assert.Contains(ValidadorPrescripcion.EjercicioRepetido, Codigos(new[] { Linea(1, ejercicio: id), Linea(2, ejercicio: id) }));
        }

        [Fact]
        public void Las_violaciones_traen_codigo_texto_y_orden()
        {
            var v = Assert.Single(ValidadorPrescripcion.Validar(new[] { Linea(3, series: 9) }, null, L));
            Assert.Equal((ValidadorPrescripcion.Series, 3), (v.Codigo, v.Orden));
            Assert.Contains("9 series", v.Mensaje);
        }
    }
}
