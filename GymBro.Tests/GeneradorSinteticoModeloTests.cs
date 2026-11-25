using GymBro.Application.Sintetico;
using Xunit;

namespace GymBro.Tests
{
    /// <summary>Etapa 7e: azar reproducible y funciones del modelo generador (valores esperados de los parametros).</summary>
    public class GeneradorSinteticoModeloTests
    {
        private static readonly ParametrosSinteticos P = new();

        [Fact]
        public void La_misma_semilla_y_flujo_dan_la_misma_secuencia_y_otro_flujo_una_distinta()
        {
            double[] Serie(int semilla, string flujo)
            {
                var a = new AzarDeterminista(semilla, flujo);
                return Enumerable.Range(0, 20).Select(_ => a.Normal(0, 1)).ToArray();
            }
            Assert.Equal(Serie(42, "socio:0"), Serie(42, "socio:0"));
            Assert.NotEqual(Serie(42, "socio:0"), Serie(42, "socio:1"));
            Assert.NotEqual(Serie(42, "socio:0"), Serie(43, "socio:0"));
        }

        [Fact]
        public void Los_ids_derivados_son_estables_y_distintos_por_texto_y_semilla()
        {
            Assert.Equal(AzarDeterminista.IdDerivado(42, "gimnasio"), AzarDeterminista.IdDerivado(42, "gimnasio"));
            Assert.NotEqual(AzarDeterminista.IdDerivado(42, "gimnasio"), AzarDeterminista.IdDerivado(43, "gimnasio"));
            Assert.NotEqual(AzarDeterminista.IdDerivado(42, "socio:0"), AzarDeterminista.IdDerivado(42, "socio:1"));
        }

        [Theory]
        [InlineData(0.0, 0.5)]
        [InlineData(1.0, 0.841345)]
        [InlineData(-1.96, 0.024998)]
        [InlineData(2.5, 0.993790)]
        public void Phi_coincide_con_la_tabla_normal(double x, double esperado)
        {
            Assert.Equal(esperado, ModeloSintetico.Phi(x), 5);
        }

        [Theory]
        [InlineData(8, 12)]
        [InlineData(10, 15)]
        [InlineData(15, 20)]
        public void La_probabilidad_de_repeticiones_en_rango_coincide_con_una_simulacion(int min, int max)
        {
            var a = new AzarDeterminista(1, $"rango:{min}");
            const int n = 200_000;
            var dentro = 0;
            for (var i = 0; i < n; i++)
            {
                var r = ModeloSintetico.Repeticiones(a.Normal(0, 1), min, max, P);
                if (r >= min && r <= max) dentro++;
            }
            Assert.Equal(ModeloSintetico.ProbabilidadRepeticionesEnRango(min, max, P), dentro / (double)n, 2);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void La_esperanza_marginal_del_desvio_RIR_coincide_con_una_simulacion(int rirObjetivo)
        {
            var a = new AzarDeterminista(2, $"rir:{rirObjetivo}");
            const int n = 200_000;
            double suma = 0;
            for (var i = 0; i < n; i++)
            {
                var fatiga = ModeloSintetico.Fatiga(a.Normal(0, 1), P);
                var ingesta = a.Categorica(P.ProporcionIngesta);
                suma += ModeloSintetico.Rir(ModeloSintetico.MediaRir(rirObjetivo, fatiga, ingesta, P), a.Normal(0, 1), P) - rirObjetivo;
            }
            Assert.Equal(ModeloSintetico.EsperanzaDesvioRirMarginal(rirObjetivo, P), suma / n, 2);
        }

        [Fact]
        public void El_riesgo_de_abandono_crece_con_la_inadherencia_y_la_morosidad()
        {
            var base0 = ModeloSintetico.RiesgoAbandono(1.0, false, P);
            Assert.True(ModeloSintetico.RiesgoAbandono(0.5, false, P) > base0);
            Assert.True(ModeloSintetico.RiesgoAbandono(1.0, true, P) > base0);
            Assert.Equal(1 / (1 + Math.Exp(5.0)), base0, 10);
        }

        [Fact]
        public void La_carga_esperada_compone_la_ganancia_semanal()
        {
            Assert.Equal(100 * Math.Pow(1.02, 8), ModeloSintetico.CargaEsperada(100, 0.02, 8), 10);
        }

        [Fact]
        public void Los_parametros_invalidos_se_rechazan()
        {
            Assert.Throws<ArgumentException>(() => new ParametrosSinteticos { Inicio = new DateOnly(2026, 1, 6) }.Validar());
            Assert.Throws<ArgumentException>(() => new ParametrosSinteticos { Socios = 0 }.Validar());
            new ParametrosSinteticos().Validar();
        }
    }
}
