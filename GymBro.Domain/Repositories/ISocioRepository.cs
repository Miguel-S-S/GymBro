using GymBro.Domain.Entities;

namespace GymBro.Domain.Repositories
{
    public interface ISocioRepository
    {
        Task<IEnumerable<Socio>> GetAllAsync();
        Task<Socio?> GetByIdAsync(Guid id);
        Task<Socio> AddAsync(Socio socio);
        Task<bool> ExistsAsync(Guid id);

        /// <summary>Socios asignados a un Personal Trainer.</summary>
        Task<IEnumerable<Socio>> GetByEntrenadorAsync(Guid entrenadorUsuarioId);
        Task<bool> ExisteDniAsync(string dni);
        /// <summary>Carga el socio para modificarlo (con tracking).</summary>
        Task<Socio?> GetForUpdateAsync(Guid id);
        Task SaveChangesAsync();
    }
}