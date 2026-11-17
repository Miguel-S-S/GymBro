using GymBro.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace GymBro.Infrastructure.Persistence
{
    /// <summary>
    /// Impide persistir un enum con un valor no definido (por ejemplo, el 0 de un enum que no se asignó).
    /// Los enums se guardan como texto, así que sin esta guarda un 0 quedaba en la base como "0".
    /// Complementa las restricciones CHECK de la base (fix-nivel-socio).
    /// </summary>
    public static class GuardaEnums
    {
        public static void Validar(ChangeTracker tracker)
        {
            foreach (var entrada in tracker.Entries())
            {
                if (entrada.State is not (EntityState.Added or EntityState.Modified)) continue;

                foreach (var propiedad in entrada.Properties)
                {
                    var tipo = Nullable.GetUnderlyingType(propiedad.Metadata.ClrType) ?? propiedad.Metadata.ClrType;
                    if (!tipo.IsEnum || propiedad.CurrentValue is null) continue;

                    if (!Enum.IsDefined(tipo, propiedad.CurrentValue))
                        throw new ReglaNegocioException(
                            $"Valor no válido para {entrada.Metadata.ClrType.Name}.{propiedad.Metadata.Name}.");
                }
            }
        }

        /// <summary>Condición SQL que admite solo los nombres definidos del enum (columna guardada como texto).</summary>
        public static string CondicionTexto<T>(string columna, bool admiteNulo = false) where T : struct, Enum
        {
            var nombres = string.Join(", ", Enum.GetNames<T>().Select(n => $"'{n}'"));
            return (admiteNulo ? $"{columna} IS NULL OR " : "") + $"{columna} IN ({nombres})";
        }
    }
}
