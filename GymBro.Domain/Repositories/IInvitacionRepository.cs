using GymBro.Domain.Entities;

namespace GymBro.Domain.Repositories
{
    public interface IInvitacionRepository
    {
        Task AddAsync(InvitacionSocio invitacion);

        /// <summary>
        /// Busca por hash del código ignorando el filtro de tenant (la consulta quien aún no tiene sesión).
        /// Devuelve la invitación con su Socio y Gimnasio, con tracking.
        /// </summary>
        Task<InvitacionSocio?> GetByTokenHashAsync(string tokenHash);

        /// <summary>Invitaciones vigentes de un socio del gimnasio actual (con tracking), para revocarlas.</summary>
        Task<IReadOnlyList<InvitacionSocio>> GetVigentesDeSocioAsync(Guid socioId);

        /// <summary>De los socios indicados, los que tienen una invitación vigente.</summary>
        Task<HashSet<Guid>> GetSocioIdsConInvitacionVigenteAsync(IReadOnlyCollection<Guid> socioIds);

        Task SaveChangesAsync();
    }
}
