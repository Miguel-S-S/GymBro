using System.Security.Cryptography;
using GymBro.Application.Abstractions;

namespace GymBro.Infrastructure.Security
{
    /// <summary>PBKDF2-HMAC-SHA512 con sal aleatoria. Formato: v1.iteraciones.sal.hash (Base64).</summary>
    public class HasheadorContrasena : IHasheadorContrasena
    {
        private const int Iteraciones = 210_000;
        private const int TamanoSal = 16;
        private const int TamanoHash = 32;

        public string Hash(string contrasena)
        {
            var sal = RandomNumberGenerator.GetBytes(TamanoSal);
            var hash = Rfc2898DeriveBytes.Pbkdf2(contrasena, sal, Iteraciones, HashAlgorithmName.SHA512, TamanoHash);
            return $"v1.{Iteraciones}.{Convert.ToBase64String(sal)}.{Convert.ToBase64String(hash)}";
        }

        public bool Verificar(string hash, string contrasena)
        {
            var partes = hash.Split('.');
            if (partes.Length != 4 || partes[0] != "v1" || !int.TryParse(partes[1], out var iter)) return false;
            try
            {
                var sal = Convert.FromBase64String(partes[2]);
                var esperado = Convert.FromBase64String(partes[3]);
                var actual = Rfc2898DeriveBytes.Pbkdf2(contrasena, sal, iter, HashAlgorithmName.SHA512, esperado.Length);
                return CryptographicOperations.FixedTimeEquals(actual, esperado);
            }
            catch (FormatException) { return false; }
        }
    }
}