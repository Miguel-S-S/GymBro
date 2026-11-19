using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GymBro.Domain.Entities;

namespace GymBro.Domain.Repositories
{
    /// <summary>
    /// Ejercicios desactivados que puede ver quien gestiona el catálogo (Etapa 9a-2b): todos (Administrador de la
    /// Plataforma) o solo los propios de un gimnasio (Dueño y Personal Trainer).
    /// </summary>
    public sealed record AlcanceInactivos(bool Todos, Guid? GimnasioId);

    public interface IEjercicioRepository
    {
        Task<bool> ExistenTodosAsync(IEnumerable<Guid> ids);

        /// <summary>
        /// Devuelve una página del catálogo ordenada por nombre, sin cargar el embedding. Con <paramref name="inactivos"/>
        /// devuelve solo los ejercicios desactivados de ese alcance (Etapa 9a-2b); sin él, los activos visibles.
        /// </summary>
        Task<(IReadOnlyList<Ejercicio> Items, int Total)> GetPaginadoAsync(
            string? texto, string? zonaObjetivo, int omitir, int tomar, GymBro.Domain.Enums.OrigenEjercicio? origen = null,
            AlcanceInactivos? inactivos = null);

        /// <summary>
        /// Devuelve el ejercicio sin cargar el embedding: el activo visible o, si se indica el alcance, también el
        /// desactivado de ese alcance.
        /// </summary>
        Task<Ejercicio?> GetByIdAsync(Guid id, AlcanceInactivos? inactivos = null);

        Task<IReadOnlyList<string>> GetZonasObjetivoAsync();

        // ---- Etapa 9a-2 y 9a-2b: gestión de ejercicios ----

        /// <summary>
        /// Ejercicio con seguimiento para editarlo, desactivarlo o reactivarlo. Ignora el filtro global (incluye los
        /// desactivados y los de cualquier gimnasio, pero nunca los dados de baja): la visibilidad la decide el servicio.
        /// </summary>
        Task<Ejercicio?> GetParaGestionAsync(Guid id);

        /// <summary>Id y nombre de los ejercicios no dados de baja (activos o desactivados) del gimnasio y globales.</summary>
        Task<IReadOnlyList<(Guid Id, string Nombre)>> GetNombresVisiblesAsync(Guid gimnasioId);

        /// <summary>Indica si alguna rutina vigente o pendiente (Aprobada, Editada, PendienteRevision) usa el ejercicio.</summary>
        Task<bool> EnUsoPorPrescripcionVigenteAsync(Guid ejercicioId);

        /// <summary>Nombres de los ejercicios propios activos del gimnasio que usan alguno de esos tipos, ordenados.</summary>
        Task<IReadOnlyList<string>> GetPropiosActivosPorTiposAsync(Guid gimnasioId, IReadOnlyCollection<Guid> tipos);

        /// <summary>Cantidad de ejercicios no dados de baja (activos o desactivados, de cualquier gimnasio) con ese tipo.</summary>
        Task<int> ContarPorTipoAsync(Guid tipoEquipamientoId);

        /// <summary>Copia el nombre del tipo al texto de equipamiento de los ejercicios propios que lo usan.</summary>
        Task ActualizarTextoEquipamientoAsync(Guid tipoEquipamientoId, string nombre);

        Task AddAsync(Ejercicio ejercicio);
        Task SaveChangesAsync();
    }
}
