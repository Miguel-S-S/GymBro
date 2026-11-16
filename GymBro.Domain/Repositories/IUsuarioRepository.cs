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

        /// <summary>Cuenta de acceso de un socio (ignora el filtro de tenant: la cuenta del socio no pertenece a un gimnasio).</summary>
        Task<Usuario?> GetBySocioIdAsync(Guid socioId);

        /// <summary>De los socios indicados, los que ya tienen cuenta de acceso.</summary>
        Task<HashSet<Guid>> GetSocioIdsConCuentaAsync(IReadOnlyCollection<Guid> socioIds);
    }
}