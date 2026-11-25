using System.Security.Cryptography;
using System.Text;

namespace GymBro.Application.Sintetico
{
    /// <summary>
    /// Etapa 7e: azar reproducible. Cada socio usa su propia secuencia derivada de (semilla, indice), asi el resultado de
    /// un socio no depende del orden ni de la cantidad de los demas. System.Random con semilla usa el algoritmo de
    /// compatibilidad, estable entre versiones de .NET.
    /// </summary>
    public sealed class AzarDeterminista
    {
        private readonly Random _r;

        public AzarDeterminista(int semilla, string flujo)
        {
            var h = SHA256.HashData(Encoding.UTF8.GetBytes($"{semilla}:{flujo}"));
            _r = new Random(BitConverter.ToInt32(h, 0) & int.MaxValue);
        }

        public double Uniforme() => _r.NextDouble();
        public double Uniforme(double min, double max) => min + (max - min) * _r.NextDouble();
        public int Entero(int minIncluido, int maxExcluido) => _r.Next(minIncluido, maxExcluido);
        public bool Bernoulli(double p) => _r.NextDouble() < p;

        /// <summary>Normal por Box-Muller (consume siempre dos uniformes).</summary>
        public double Normal(double media, double desvio)
        {
            var u1 = 1.0 - _r.NextDouble();
            var u2 = _r.NextDouble();
            return media + desvio * Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }

        /// <summary>Indice elegido con las probabilidades dadas (suman 1).</summary>
        public int Categorica(IReadOnlyList<double> probabilidades)
        {
            var u = _r.NextDouble();
            double acumulada = 0;
            for (var i = 0; i < probabilidades.Count; i++)
            {
                acumulada += probabilidades[i];
                if (u < acumulada) return i;
            }
            return probabilidades.Count - 1;
        }

        /// <summary>Identificador derivado de la semilla: mismo texto y semilla, mismo Guid.</summary>
        public static Guid IdDerivado(int semilla, string texto)
        {
            var h = SHA256.HashData(Encoding.UTF8.GetBytes($"gymbro-sintetico:{semilla}:{texto}"));
            var b = h[..16];
            b[7] = (byte)((b[7] & 0x0F) | 0x80); // version 8 (definida por la aplicacion)
            b[8] = (byte)((b[8] & 0x3F) | 0x80); // variante RFC 4122
            return new Guid(b);
        }
    }

    /// <summary>
    /// Etapa 7e: funciones del modelo generador, puras, que usan el generador y las pruebas (para comparar lo observado con
    /// el valor esperado de los parametros conocidos).
    /// </summary>
    public static class ModeloSintetico
    {
        /// <summary>Carga verdadera (sin ruido) despues de t semanas con ganancia compuesta g.</summary>
        public static double CargaEsperada(double cargaInicial, double ganancia, double semanas) =>
            cargaInicial * Math.Pow(1 + ganancia, semanas);

        public static int Recortar(int v, int min, int max) => Math.Max(min, Math.Min(max, v));

        /// <summary>Repeticiones de una serie: N(centro + desplazamiento, desvio) redondeada, entre 1 y maximo + 5.</summary>
        public static int Repeticiones(double z, int minimo, int maximo, ParametrosSinteticos p) =>
            Recortar((int)Math.Round((minimo + maximo) / 2.0 + p.DesplazamientoRepeticiones + p.DesvioRepeticiones * z,
                MidpointRounding.AwayFromZero), 1, maximo + 5);

        /// <summary>Media del RIR (antes del ruido) segun objetivo, fatiga e ingesta del dia.</summary>
        public static double MediaRir(int rirObjetivo, double fatiga, int indiceIngesta, ParametrosSinteticos p) =>
            rirObjetivo + p.SesgoRir + p.EfectoFatigaSobreRir * (fatiga - 4.0) + p.EfectoIngestaSobreRir[indiceIngesta];

        public static int Rir(double media, double z, ParametrosSinteticos p) =>
            Recortar((int)Math.Round(media + p.DesvioRir * z, MidpointRounding.AwayFromZero), 0, 5);

        /// <summary>Probabilidad de que round(N(mu, s)) caiga en [a, b] (con el redondeo y el recorte del generador).</summary>
        public static double ProbabilidadRepeticionesEnRango(int minimo, int maximo, ParametrosSinteticos p)
        {
            var mu = (minimo + maximo) / 2.0 + p.DesplazamientoRepeticiones;
            // round(x) en [a, b] con redondeo "lejos de cero" para positivos <=> x en [a - 0,5, b + 0,5).
            return Phi((maximo + 0.5 - mu) / p.DesvioRepeticiones) - Phi((minimo - 0.5 - mu) / p.DesvioRepeticiones);
        }

        /// <summary>Esperanza de (RIR registrado - objetivo) dada la media del RIR, con redondeo y recorte a [0, 5].</summary>
        public static double EsperanzaDesvioRir(int rirObjetivo, double media, ParametrosSinteticos p)
        {
            double e = 0;
            for (var k = 0; k <= 5; k++)
            {
                var inferior = k == 0 ? double.NegativeInfinity : (k - 0.5 - media) / p.DesvioRir;
                var superior = k == 5 ? double.PositiveInfinity : (k + 0.5 - media) / p.DesvioRir;
                e += k * (Phi(superior) - Phi(inferior));
            }
            return e - rirObjetivo;
        }

        /// <summary>Fatiga latente del dia: N(media, desvio) redondeada y recortada a la escala 1 a 7.</summary>
        public static int Fatiga(double z, ParametrosSinteticos p) =>
            Recortar((int)Math.Round(p.FatigaMedia + p.FatigaDesvio * z, MidpointRounding.AwayFromZero), 1, 7);

        /// <summary>Probabilidad de cada valor de fatiga (1 a 7) segun el modelo.</summary>
        public static double ProbabilidadFatiga(int f, ParametrosSinteticos p)
        {
            var inferior = f == 1 ? double.NegativeInfinity : (f - 0.5 - p.FatigaMedia) / p.FatigaDesvio;
            var superior = f == 7 ? double.PositiveInfinity : (f + 0.5 - p.FatigaMedia) / p.FatigaDesvio;
            return Phi(superior) - Phi(inferior);
        }

        /// <summary>
        /// Esperanza de (RIR registrado - objetivo) promediando sobre la fatiga y la ingesta latentes del dia: es el valor que
        /// deberia recuperar el desvio RIR medio de K3 con muchos datos.
        /// </summary>
        public static double EsperanzaDesvioRirMarginal(int rirObjetivo, ParametrosSinteticos p)
        {
            double e = 0;
            for (var f = 1; f <= 7; f++)
                for (var i = 0; i < p.ProporcionIngesta.Length; i++)
                    e += ProbabilidadFatiga(f, p) * p.ProporcionIngesta[i] * EsperanzaDesvioRir(rirObjetivo, MediaRir(rirObjetivo, f, i, p), p);
            return e;
        }

        /// <summary>Funcion de distribucion normal estandar (aproximacion de Abramowitz y Stegun 7.1.26, error menor a 1,5e-7).</summary>
        public static double Phi(double x)
        {
            if (double.IsNegativeInfinity(x)) return 0;
            if (double.IsPositiveInfinity(x)) return 1;
            var t = 1.0 / (1.0 + 0.3275911 * Math.Abs(x) / Math.Sqrt(2));
            var y = 1.0 - (((((1.061405429 * t - 1.453152027) * t) + 1.421413741) * t - 0.284496736) * t + 0.254829592) * t
                * Math.Exp(-x * x / 2);
            return x >= 0 ? 0.5 * (1 + y) : 0.5 * (1 - y);
        }

        /// <summary>Riesgo semanal de abandono (funcion logistica).</summary>
        public static double RiesgoAbandono(double adherenciaReciente, bool moroso, ParametrosSinteticos p) =>
            1.0 / (1.0 + Math.Exp(-(p.AbandonoB0 + p.AbandonoB1 * (1 - adherenciaReciente) + p.AbandonoB2 * (moroso ? 1 : 0))));
    }
}
