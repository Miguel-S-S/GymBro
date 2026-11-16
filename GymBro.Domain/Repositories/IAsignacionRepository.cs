using GymBro.Domain.Entities;

namespace GymBro.Domain.Repositories
{
    public interface IAsignacionRepository
    {
        Task<bool> EstaAsignadoAsync(Guid entrenadorUsuarioId, Guid socioId);
        Task<IEnumerable<Guid>> GetSocioIdsAsync(Guid entrenadorUsuarioId);
        Task AddAsync(AsignacionEntrenadorSocio asignacion);
        Task<bool> ExisteAsync(Guid entrenadorUsuarioId, Guid socioId);

        /// <summary>Marca para eliminar todas las asignaciones del socio. No guarda: se confirma con el SaveChanges de la misma unidad de trabajo.</summary>
        Task QuitarDeSocioAsync(Guid socioId);
    }
}