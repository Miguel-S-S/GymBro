using GymBro.Domain.Entities;

namespace GymBro.Domain.Repositories
{
    public interface IAsignacionRepository
    {
        Task<bool> EstaAsignadoAsync(Guid entrenadorUsuarioId, Guid socioId);
        Task<IEnumerable<Guid>> GetSocioIdsAsync(Guid entrenadorUsuarioId);
        Task AddAsync(AsignacionEntrenadorSocio asignacion);
        Task<bool> ExisteAsync(Guid entrenadorUsuarioId, Guid socioId);
    }
}