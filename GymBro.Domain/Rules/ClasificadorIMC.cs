using GymBro.Domain.Enums;

namespace GymBro.Domain.Rules
{
    /// <summary>
    /// Regla determinista que traduce el IMC (decimal) de la última medición a la categoría
    /// con la que se consulta la matriz de recomendaciones. Umbrales de la OMS:
    /// &lt; 18,5 bajo peso; 18,5–24,9 normal; 25–29,9 sobrepeso; ≥ 30 obesidad.
    /// </summary>
    public static class ClasificadorIMC
    {
        /// <summary>IMC = peso (kg) / altura (m)². Devuelve null si faltan datos válidos.</summary>
        public static decimal? Calcular(decimal pesoKg, decimal alturaCm)
        {
            if (pesoKg <= 0 || alturaCm <= 0) return null;
            var metros = alturaCm / 100m;
            return Math.Round(pesoKg / (metros * metros), 2);
        }

        public static CategoriaIMC Clasificar(decimal imc) =>
            imc < 18.5m ? CategoriaIMC.BajoPeso
            : imc < 25m ? CategoriaIMC.Normal
            : imc < 30m ? CategoriaIMC.Sobrepeso
            : CategoriaIMC.Obeso;
    }
}
