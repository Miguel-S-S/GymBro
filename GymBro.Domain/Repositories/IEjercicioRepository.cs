using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GymBro.Domain.Entities;

namespace GymBro.Domain.Repositories
{
    public interface IEjercicioRepository
    {
        Task<bool> ExistenTodosAsync(IEnumerable<Guid> ids);

        /// <summary>
        /// Devuelve una página del catálogo ordenada por nombre, sin cargar el embedding.
        /// </summary>
        Task<(IReadOnlyList<Ejercicio> Items, int Total)> GetPaginadoAsync(
            string? texto, string? zonaObjetivo, int omitir, int tomar);

        /// <summary>
        /// Devuelve el ejercicio sin cargar el embedding.
        /// </summary>
        Task<Ejercicio?> GetByIdAsync(Guid id);

        Task<IReadOnlyList<string>> GetZonasObjetivoAsync();
    }
}
