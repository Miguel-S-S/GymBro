using GymBro.Domain.Entities;

namespace GymBro.Domain.Repositories
{
    public interface ISocioRepository
    {
        Task<IEnumerable<Socio>> GetAllAsync();
        Task<Socio> AddAsync(Socio socio);
    }
}
