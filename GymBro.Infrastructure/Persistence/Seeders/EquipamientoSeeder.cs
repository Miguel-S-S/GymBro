using GymBro.Domain.Rules;
using Microsoft.EntityFrameworkCore;

namespace GymBro.Infrastructure.Persistence.Seeders
{
    /// <summary>
    /// Tipos globales de equipamiento y su asignación a los ejercicios globales (Etapa 9a-2).
    /// Se invoca al arrancar en todos los entornos, después de CatalogoProyectoSeeder.
    /// Idempotente:
    ///   - inserta los tipos globales cuyo Id fijo todavía no existe (incluidos los dados de baja: no los reactiva);
    ///   - asigna TipoEquipamientoId solo a los ejercicios globales que lo tienen nulo (nunca pisa una asignación).
    /// Un texto de equipamiento sin regla en CatalogoEquipamiento lanza EquipamientoDesconocidoException: no se asigna
    /// nada en silencio. Los ejercicios propios de un gimnasio reciben su tipo al crearse, así que no se tocan.
    /// </summary>
    public static class EquipamientoSeeder
    {
        /// <summary>Resultado: tipos insertados y ejercicios con tipo asignado, por clave.</summary>
        public record Resultado(int TiposInsertados, IReadOnlyDictionary<string, int> AsignadosPorClave)
        {
            public int EjerciciosAsignados => AsignadosPorClave.Values.Sum();
        }

        public static async Task<Resultado> SeedAsync(GymBroDbContext context)
        {
            var tipos = CatalogoEquipamiento.TiposGlobales();
            var ids = tipos.Select(t => t.Id).ToArray();
            var existentes = await context.TiposEquipamiento.IgnoreQueryFilters()
                .Where(t => ids.Contains(t.Id)).Select(t => t.Id).ToListAsync();
            var nuevos = tipos.Where(t => !existentes.Contains(t.Id)).ToList();
            if (nuevos.Count > 0)
            {
                context.TiposEquipamiento.AddRange(nuevos);
                await context.SaveChangesAsync();
            }

            // Solo Id y texto: no se carga el embedding.
            var pendientes = await context.Ejercicios.IgnoreQueryFilters()
                .Where(e => e.TipoEquipamientoId == null && e.GimnasioId == null)
                .Select(e => new { e.Id, e.Equipamiento })
                .ToListAsync();

            // Primero se mapea todo: si un valor es desconocido, se lanza antes de escribir nada.
            var porClave = pendientes
                .GroupBy(p => CatalogoEquipamiento.Mapear(p.Equipamiento))
                .ToDictionary(g => g.Key, g => g.Select(p => p.Id).ToList());

            var asignados = new Dictionary<string, int>();
            foreach (var (clave, idsEjercicios) in porClave.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                var tipoId = CatalogoEquipamiento.IdDe(clave);
                var filas = await context.Ejercicios.IgnoreQueryFilters()
                    .Where(e => idsEjercicios.Contains(e.Id) && e.TipoEquipamientoId == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(e => e.TipoEquipamientoId, tipoId));
                asignados[clave] = filas;
            }

            var resultado = new Resultado(nuevos.Count, asignados);
            if (resultado.TiposInsertados > 0 || resultado.EjerciciosAsignados > 0)
                Console.WriteLine($"[OK] Equipamiento: {resultado.TiposInsertados} tipos globales agregados y " +
                                  $"{resultado.EjerciciosAsignados} ejercicios con tipo asignado.");
            return resultado;
        }
    }
}
