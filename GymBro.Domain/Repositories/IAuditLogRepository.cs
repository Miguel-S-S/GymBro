using GymBro.Domain.Entities;
namespace GymBro.Domain.Repositories
{
    public interface IAuditLogRepository
    {
        Task AddAsync(AuditLog registro);
        Task<IEnumerable<AuditLog>> GetRecientesAsync(int cantidad, Guid? gimnasioId, DateTime? desdeUtc = null, DateTime? hastaUtc = null, string? accion = null, string? entidad = null);
    }
}