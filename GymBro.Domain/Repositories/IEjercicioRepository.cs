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
            string? texto, string? zonaObjetivo, int omitir, int tomar, GymBro.Domain.Enums.OrigenEjercicio? origen = null);

        /// <summary>
        /// Devuelve el ejercicio sin cargar el embedding.
        /// </summary>
        Task<Ejercicio?> GetByIdAsync(Guid id);

        Task<IReadOnlyList<string>> GetZonasObjetivoAsync();

        // ---- Etapa 9a-2: ejercicios propios del gimnasio ----

        /// <summary>Ejercicio con seguimiento, para editarlo o darlo de baja (pasa por el filtro de visibilidad).</summary>
        Task<Ejercicio?> GetParaEdicionAsync(Guid id);

        /// <summary>Id y nombre de los ejercicios vigentes visibles para el gimnasio: globales y propios.</summary>
        Task<IReadOnlyList<(Guid Id, string Nombre)>> GetNombresVisiblesAsync(Guid gimnasioId);

        /// <summary>Indica si alguna rutina vigente o pendiente (Aprobada, Editada, PendienteRevision) usa el ejercicio.</summary>
        Task<bool> EnUsoPorPrescripcionVigenteAsync(Guid ejercicioId);

        Task AddAsync(Ejercicio ejercicio);
        Task SaveChangesAsync();
    }
}
