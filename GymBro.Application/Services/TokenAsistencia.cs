using System.Security.Cryptography;
using System.Text;

namespace GymBro.Application.Services
{
    /// <summary>
    /// Etapa 11: configuracion del QR de asistencia (seccion "Asistencia"). La clave es un secreto: va en user-secrets o
    /// en la variable de entorno Asistencia__ClaveQr, nunca en el repositorio. Sin clave valida el QR queda deshabilitado
    /// (el registro manual sigue disponible).
    /// </summary>
    public class AsistenciaConfig
    {
        public const string Seccion = "Asistencia";
        public const int LargoMinimoClave = 32;

        public string ClaveQr { get; set; } = string.Empty;

        /// <summary>Duracion del token del QR. Pedido de la etapa: 2 minutos.</summary>
        public int DuracionTokenSegundos { get; set; } = 120;

        public bool QrHabilitado => Encoding.UTF8.GetByteCount(ClaveQr ?? string.Empty) >= LargoMinimoClave;
    }

    /// <summary>
    /// Etapa 11: token firmado de corta duracion que la PWA del Socio dibuja como QR.
    /// Formato binario: version (1 byte) + id del socio (16) + vencimiento en segundos Unix (8) + HMAC-SHA256 truncado a 12
    /// bytes de los 25 anteriores; todo en Base32 (RFC 4648, sin relleno), que entra en el modo alfanumerico del QR y se
    /// puede tipear. Los guiones y espacios se ignoran al validar. No lleva el gimnasio: quien lo valida solo encuentra al
    /// socio si pertenece a su propio gimnasio (filtro entre gimnasios).
    /// </summary>
    public static class TokenAsistencia
    {
        private const byte Version = 1;
        private const int LargoMac = 12;
        private const int LargoCuerpo = 1 + 16 + 8;
        private const string Alfabeto = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

        public static string Generar(Guid socioId, DateTimeOffset vence, string clave)
        {
            var cuerpo = new byte[LargoCuerpo];
            cuerpo[0] = Version;
            socioId.ToByteArray().CopyTo(cuerpo, 1);
            BitConverter.GetBytes(vence.ToUnixTimeSeconds()).CopyTo(cuerpo, 17);
            if (!BitConverter.IsLittleEndian) Array.Reverse(cuerpo, 17, 8);
            var mac = Firmar(cuerpo, clave);
            return ABase32([.. cuerpo, .. mac]);
        }

        /// <summary>Resultado de validar un token.</summary>
        public enum Resultado { Valido, Malformado, FirmaInvalida, Vencido }

        public static Resultado Validar(string? token, DateTimeOffset ahora, string clave, out Guid socioId)
        {
            socioId = Guid.Empty;
            var bytes = DeBase32(token);
            if (bytes is null || bytes.Length != LargoCuerpo + LargoMac || bytes[0] != Version) return Resultado.Malformado;

            var cuerpo = bytes[..LargoCuerpo];
            var esperado = Firmar(cuerpo, clave);
            if (!CryptographicOperations.FixedTimeEquals(esperado, bytes.AsSpan(LargoCuerpo))) return Resultado.FirmaInvalida;

            var segundos = cuerpo[17..25];
            if (!BitConverter.IsLittleEndian) Array.Reverse(segundos);
            var vence = DateTimeOffset.FromUnixTimeSeconds(BitConverter.ToInt64(segundos));
            if (ahora > vence) return Resultado.Vencido;

            socioId = new Guid(cuerpo.AsSpan(1, 16));
            return Resultado.Valido;
        }

        private static byte[] Firmar(byte[] cuerpo, string clave) =>
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(clave), cuerpo)[..LargoMac];

        private static string ABase32(byte[] datos)
        {
            var sb = new StringBuilder((datos.Length * 8 + 4) / 5);
            int buffer = 0, bits = 0;
            foreach (var b in datos)
            {
                buffer = (buffer << 8) | b;
                bits += 8;
                while (bits >= 5)
                {
                    sb.Append(Alfabeto[(buffer >> (bits - 5)) & 31]);
                    bits -= 5;
                }
            }
            if (bits > 0) sb.Append(Alfabeto[(buffer << (5 - bits)) & 31]);
            return sb.ToString();
        }

        private static byte[]? DeBase32(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto) || texto.Length > 200) return null;
            var salida = new List<byte>(texto.Length * 5 / 8);
            int buffer = 0, bits = 0;
            foreach (var c0 in texto)
            {
                if (c0 is '-' or ' ') continue;
                var v = Alfabeto.IndexOf(char.ToUpperInvariant(c0));
                if (v < 0) return null;
                buffer = (buffer << 5) | v;
                bits += 5;
                if (bits >= 8)
                {
                    salida.Add((byte)((buffer >> (bits - 8)) & 0xFF));
                    bits -= 8;
                }
            }
            return salida.ToArray();
        }
    }
}
