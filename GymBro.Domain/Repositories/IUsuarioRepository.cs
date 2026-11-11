using GymBro.Domain.Entities;

namespace GymBro.Domain.Repositories
{
    public interface IUsuarioRepository
    {
        /// <summary>Busca ignorando el filtro de tenant (se usa para login y unicidad de email).</summary>
        Task<Usuario?> GetByEmailAsync(string emailNormalizado);
        Task<Usuario?> GetByIdAsync(Guid id);
        Task<IEnumerable<Usuario>> GetEntrenadoresAsync();
        Task AddAsync(Usuario usuario);
    }
}