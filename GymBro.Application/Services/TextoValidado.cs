using System.Globalization;
using System.Text.RegularExpressions;
using GymBro.Domain.Exceptions;

namespace GymBro.Application.Services
{
    /// <summary>
    /// Validación y sanitización mínima de los textos que carga el Dueño (Etapa 9a-2). Esos textos se vectorizan y
    /// alimentarán el RAG de la 9b, así que se limitan en longitud y se rechazan los caracteres de control y los
    /// invisibles de formato (ancho cero, marcas de dirección), que pueden ocultar instrucciones al modelo.
    /// No es una defensa completa contra la inyección de instrucciones: es un mínimo, documentado como tal.
    /// </summary>
    public static class TextoValidado
    {
        public const int LargoNombre = 120;
        public const int LargoTextoLibre = 2000;

        private static readonly Regex Espacios = new(@"[ \t]+", RegexOptions.Compiled);

        /// <summary>
        /// Recorta, colapsa espacios y valida. Una línea: sin saltos. Multilínea: admite saltos de línea.
        /// Devuelve "" si el valor es opcional y vino vacío.
        /// </summary>
        public static string Validar(string? valor, string campo, int largoMaximo, bool obligatorio, bool multilinea = false)
        {
            var texto = (valor ?? string.Empty).Replace("\r\n", "\n").Trim();
            foreach (var c in texto)
            {
                var categoria = CharUnicodeInfo.GetUnicodeCategory(c);
                if (c == '\n' && multilinea) continue;
                if (char.IsControl(c) && !(c == '\t'))
                    throw new ReglaNegocioException($"{campo}: contiene caracteres de control no permitidos.");
                if (categoria == UnicodeCategory.Format)
                    throw new ReglaNegocioException($"{campo}: contiene caracteres invisibles no permitidos.");
            }
            texto = Espacios.Replace(texto, " ");
            if (multilinea) texto = string.Join("\n", texto.Split('\n').Select(l => l.Trim()));

            if (obligatorio && texto.Length == 0)
                throw new ReglaNegocioException($"{campo} es obligatorio.");
            if (texto.Length > largoMaximo)
                throw new ReglaNegocioException($"{campo} no puede superar los {largoMaximo} caracteres.");
            return texto;
        }
    }
}
