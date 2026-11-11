using GymBro.Domain.Entities;

namespace GymBro.Domain.Repositories
{
    public interface IGimnasioRepository
    {
        Task<Gimnasio?> GetByIdAsync(Guid id);
        Task<Gimnasio?> GetByCodigoAsync(string codigo);
        Task<bool> ExisteCodigoAsync(string codigo);
        Task<IEnumerable<Gimnasio>> GetAllAsync();
        Task AddAsync(Gimnasio gimnasio, Usuario dueno);
        /// <summary>Política del gimnasio; ignora el filtro de tenant porque la consulta un socio.</summary>
        Task<bool> RequiereAprobacionAsync(Guid gimnasioId);
        Task SaveChangesAsync();
    }
}