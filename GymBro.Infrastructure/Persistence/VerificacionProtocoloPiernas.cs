using GymBro.Domain.Rules;
using Microsoft.EntityFrameworkCore;

namespace GymBro.Infrastructure.Persistence
{
    /// <summary>
    /// Etapa 7d: verificación de arranque del catálogo del protocolo de piernas. No siembra ni modifica nada: la siembra
    /// la hace la migración Etapa7d_ProtocoloPiernas y solo encuentra los ejercicios en la base del autor (los UUID del
    /// catálogo curado). En otra base el catálogo queda vacío y el generador falla con un mensaje que indica el grupo.
    /// </summary>
    public static class VerificacionProtocoloPiernas
    {
        /// <summary>Descripción de lo que falta, o null si cada grupo de la composición tiene filas con ejercicio activo.</summary>
        public static async Task<string?> DescribirFaltantesAsync(GymBroDbContext context, ProtocoloPiernasConfig config)
        {
            var filas = await (
                from p in context.EjerciciosProtocolo.AsNoTracking()
                where p.ProtocoloCodigo == ProtocoloPiernasConfig.ProtocoloCodigo && p.ProtocoloVersion == config.Version
                select new
                {
                    p.Grupo,
                    Activo = context.Ejercicios.IgnoreQueryFilters().Any(e => e.Id == p.EjercicioId && e.FechaBaja == null && e.Activo),
                }).ToListAsync();

            var problemas = new List<string>();
            if (filas.Count == 0)
                problemas.Add($"no hay filas en ejercicios_protocolo para {config.Version}");
            var inactivas = filas.Count(f => !f.Activo);
            if (inactivas > 0)
                problemas.Add($"{inactivas} filas apuntan a ejercicios inactivos o dados de baja");
            var sinGrupo = config.ComposicionEfectiva().Select(c => c.Grupo).Distinct()
                .Where(g => !filas.Any(f => f.Grupo == g && f.Activo))
                .Select(PlanificadorPiernas.NombreGrupo).ToList();
            if (filas.Count > 0 && sinGrupo.Count > 0)
                problemas.Add($"grupos sin ejercicios activos: {string.Join(", ", sinGrupo)}");

            return problemas.Count == 0 ? null : string.Join("; ", problemas) + ".";
        }
    }
}
