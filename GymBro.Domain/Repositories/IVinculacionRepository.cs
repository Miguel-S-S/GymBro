using GymBro.Domain.Entities;

namespace GymBro.Domain.Repositories
{
    public interface IVinculacionRepository
    {
        Task<SolicitudVinculacion?> GetByIdAsync(Guid id);
        Task<IEnumerable<SolicitudVinculacion>> GetPendientesDeGimnasioAsync(Guid gimnasioId);
        Task<IEnumerable<SolicitudVinculacion>> GetDeSocioAsync(Guid socioId);
        Task<bool> ExistePendienteAsync(Guid socioId, Guid gimnasioId);
        Task AddAsync(SolicitudVinculacion solicitud);
        Task SaveChangesAsync();
    }
}