using GymBro.Domain.Entities;

namespace GymBro.Domain.Repositories
{
    public interface ISocioRepository
    {
        Task<IEnumerable<Socio>> GetAllAsync();
        Task<Socio?> GetByIdAsync(Guid id);
        Task<Socio> AddAsync(Socio socio);
        Task<bool> ExistsAsync(Guid id);
    }
}
