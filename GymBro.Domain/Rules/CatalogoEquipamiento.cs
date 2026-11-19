using System.Globalization;
using System.Text;
using GymBro.Domain.Entities;

namespace GymBro.Domain.Rules
{
    /// <summary>
    /// Tipos globales de equipamiento y mapeo del texto libre del dataset a esos tipos (Etapa 9a-2).
    ///
    /// ADVERTENCIA: la tabla de tipos y el mapeo son una PROPUESTA DE DISEÑO, no una clasificación validada por un
    /// profesional. Tres casos son provisionales (decisión abierta D1): Lever/Sled sin aclaración (MAQUINA_OTRA),
    /// Assisted (partner) (PESO_CORPORAL) y Weighted (PESO_ADICIONAL).
    ///
    /// El mapeo son datos: una lista ordenada de reglas sobre el texto normalizado. Un valor que no coincide con ninguna
    /// regla no se asigna en silencio: Mapear lanza una excepción explícita.
    /// </summary>
    public static class CatalogoEquipamiento
    {
        public const string PesoCorporal = "PESO_CORPORAL";
        public const string Estiramiento = "ESTIRAMIENTO";
        public const string Mancuerna = "MANCUERNA";
        public const string Barra = "BARRA";
        public const string Polea = "POLEA";
        public const string Smith = "SMITH";
        public const string MaquinaSelectorizada = "MAQUINA_SELECTORIZADA";
        public const string MaquinaDiscos = "MAQUINA_DISCOS";
        public const string MaquinaOtra = "MAQUINA_OTRA";
        public const string MaquinaAsistida = "MAQUINA_ASISTIDA";
        public const string Banda = "BANDA";
        public const string Suspension = "SUSPENSION";
        public const string PesoAdicional = "PESO_ADICIONAL";

        /// <summary>Tipos globales (GimnasioId = null) con Id fijo. El orden es el de presentación.</summary>
        public static IReadOnlyList<TipoEquipamiento> TiposGlobales() => new List<TipoEquipamiento>
        {
            Tipo("de1b1cc5-2e3a-4d09-8a1e-07eb9e545835", PesoCorporal, "Peso corporal", true),
            Tipo("fe27bdba-dccf-468b-ad2e-7e86c4200fc6", Estiramiento, "Estiramiento (sin equipo)", true),
            Tipo("8f1037ce-aa04-4806-a789-faa5634cd30f", Mancuerna, "Mancuernas", false),
            Tipo("f074c56b-b849-4c46-ba64-a820830c47a4", Barra, "Barra", false),
            Tipo("90bdf22a-7650-482d-8f40-f6ce04970a12", Polea, "Polea / cable", false),
            Tipo("db085ee4-4706-44cd-8039-97bd73f4e7ec", Smith, "Máquina Smith", false),
            Tipo("caac69ff-cc79-4696-9c9f-e50b07c55ab1", MaquinaSelectorizada, "Máquina con selector de peso", false),
            Tipo("3ac2c945-6ec8-409e-8ac7-c8abbc586cf6", MaquinaDiscos, "Máquina de discos", false),
            Tipo("24925b08-8e4f-449c-b88b-e0d8d252a67f", MaquinaOtra, "Máquina (sin especificar)", false),
            Tipo("b81c5672-8f7b-459f-b65a-e628fa77dbe6", MaquinaAsistida, "Máquina asistida", false),
            Tipo("92bbf185-0258-404b-863f-8237488c2872", Banda, "Banda elástica", false),
            Tipo("0df8fd2a-8b7b-4ba4-9288-3817bbe56a36", Suspension, "Suspensión (TRX o similar)", false),
            Tipo("5f70dbb5-a68d-42d2-a37c-3d6fc09a18dd", PesoAdicional, "Peso adicional (cinturón, disco)", false),
        };

        /// <summary>Id fijo del tipo global con esa clave.</summary>
        public static Guid IdDe(string clave) =>
            TiposGlobales().FirstOrDefault(t => t.Clave == clave)?.Id
            ?? throw new ArgumentException($"No existe el tipo global de equipamiento '{clave}'.");

        public enum Coincidencia
        {
            /// <summary>El texto normalizado es igual al patrón.</summary>
            Exacta,
            /// <summary>Igual al patrón, o el patrón seguido de un espacio y un texto que no empieza con paréntesis
            /// (por ejemplo, "assisted chest dip"). Una variante entre paréntesis desconocida no coincide.</summary>
            Prefijo,
            /// <summary>El texto empieza con el patrón, sin exigir separador (por ejemplo, "stretchbroomstickdoorway").</summary>
            PrefijoLibre
        }

        public record Regla(string Patron, Coincidencia Modo, string Clave);

        /// <summary>Reglas en orden: gana la primera que coincide. Los patrones están normalizados (minúsculas).</summary>
        public static readonly IReadOnlyList<Regla> Reglas = new List<Regla>
        {
            new("assisted (partner)", Coincidencia.Exacta, PesoCorporal),      // provisional (D1)
            new("assisted (machine)", Coincidencia.Exacta, MaquinaAsistida),
            new("assisted", Coincidencia.Prefijo, MaquinaAsistida),
            new("band-assisted", Coincidencia.Exacta, Banda),
            new("self-assisted", Coincidencia.Exacta, PesoCorporal),
            new("body weight", Coincidencia.Exacta, PesoCorporal),
            new("plyometric", Coincidencia.Exacta, PesoCorporal),
            new("dynamic stretch", Coincidencia.Exacta, Estiramiento),
            new("stretch", Coincidencia.PrefijoLibre, Estiramiento),
            new("dumbbell", Coincidencia.Exacta, Mancuerna),
            new("barbell", Coincidencia.Exacta, Barra),
            new("cable (pull side)", Coincidencia.Exacta, Polea),
            new("cable", Coincidencia.Prefijo, Polea),
            new("smith", Coincidencia.Exacta, Smith),
            new("lever (selectorized)", Coincidencia.Prefijo, MaquinaSelectorizada),
            new("sled (selectorized)", Coincidencia.Exacta, MaquinaSelectorizada),
            new("lever (plate loaded)", Coincidencia.Exacta, MaquinaDiscos),
            new("sled (plate loaded)", Coincidencia.Exacta, MaquinaDiscos),
            new("lever", Coincidencia.Exacta, MaquinaOtra),                    // provisional (D1)
            new("sled", Coincidencia.Exacta, MaquinaOtra),                     // provisional (D1)
            new("suspended", Coincidencia.Exacta, Suspension),
            new("suspension", Coincidencia.Exacta, Suspension),
            new("weighted", Coincidencia.Prefijo, PesoAdicional),              // provisional (D1)
        };

        /// <summary>
        /// Quita los caracteres de ancho cero (U+200B, U+200C, U+200D, U+FEFF), colapsa espacios, recorta y pasa a minúsculas.
        /// </summary>
        public static string Normalizar(string? texto)
        {
            if (string.IsNullOrEmpty(texto)) return string.Empty;
            var sb = new StringBuilder(texto.Length);
            var espacioPendiente = false;
            foreach (var c in texto)
            {
                if (c is '​' or '‌' or '‍' or '﻿') continue;
                if (char.IsWhiteSpace(c)) { espacioPendiente = sb.Length > 0; continue; }
                if (espacioPendiente) { sb.Append(' '); espacioPendiente = false; }
                sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        /// <summary>Clave del tipo global para un texto de equipamiento, o null si ninguna regla coincide.</summary>
        public static string? IntentarMapear(string? texto)
        {
            var n = Normalizar(texto);
            if (n.Length == 0) return null;
            foreach (var r in Reglas)
            {
                var coincide = r.Modo switch
                {
                    Coincidencia.Exacta => n == r.Patron,
                    Coincidencia.PrefijoLibre => n.StartsWith(r.Patron, StringComparison.Ordinal),
                    _ => n == r.Patron
                         || (n.StartsWith(r.Patron + " ", StringComparison.Ordinal) && !n[(r.Patron.Length + 1)..].StartsWith('(')),
                };
                if (coincide) return r.Clave;
            }
            return null;
        }

        /// <summary>Clave del tipo global para un texto de equipamiento. Lanza si el valor no está contemplado.</summary>
        public static string Mapear(string? texto) =>
            IntentarMapear(texto) ?? throw new EquipamientoDesconocidoException(texto ?? string.Empty);

        /// <summary>Texto en minúsculas, sin acentos y con espacios colapsados (para comparar nombres).</summary>
        public static string ClaveDeComparacion(string? texto)
        {
            var n = Normalizar(texto).Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(n.Length);
            foreach (var c in n)
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        private static TipoEquipamiento Tipo(string id, string clave, string nombre, bool siempre) => new()
        {
            Id = Guid.Parse(id), Clave = clave, Nombre = nombre, SiempreDisponible = siempre, GimnasioId = null
        };
    }

    /// <summary>Valor de equipamiento del catálogo que no entra en ninguna regla del mapeo: requiere decisión del autor.</summary>
    public class EquipamientoDesconocidoException : Exception
    {
        public string Valor { get; }
        public EquipamientoDesconocidoException(string valor)
            : base($"Valor de equipamiento sin tipo en el mapeo: '{valor}'. Agregar una regla en CatalogoEquipamiento (decisión del autor).")
        {
            Valor = valor;
        }
    }
}
