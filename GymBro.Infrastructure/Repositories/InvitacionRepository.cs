using GymBro.Domain.Entities;
using GymBro.Domain.Repositories;
using GymBro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GymBro.Infrastructure.Repositories
{
    public class InvitacionRepository : IInvitacionRepository
    {
        private readonly GymBroDbContext _context;
        public InvitacionRepository(GymBroDbContext context) { _context = context; }

        public async Task AddAsync(InvitacionSocio invitacion)
        {
            await _context.Invitaciones.AddAsync(invitacion);
            await _context.SaveChangesAsync();
        }

        // La activación ocurre sin sesión: se ignora el filtro de tenant. El acceso lo da conocer el código.
        public Task<InvitacionSocio?> GetByTokenHashAsync(string tokenHash) =>
            _context.Invitaciones.IgnoreQueryFilters()
                .Include(i => i.Socio)
                .Include(i => i.Gimnasio)
                .FirstOrDefaultAsync(i => i.TokenHash == tokenHash && i.FechaBaja == null);

        public async Task<IReadOnlyList<InvitacionSocio>> GetVigentesDeSocioAsync(Guid socioId)
        {
            var ahora = DateTime.UtcNow;
            return await _context.Invitaciones
                .Where(i => i.SocioId == socioId && i.UsadaUtc == null && i.RevocadaUtc == null && i.ExpiraUtc > ahora)
                .ToListAsync();
        }

        public async Task<HashSet<Guid>> GetSocioIdsConInvitacionVigenteAsync(IReadOnlyCollection<Guid> socioIds)
        {
            if (socioIds.Count == 0) return new HashSet<Guid>();
            var ahora = DateTime.UtcNow;
            var ids = await _context.Invitaciones
                .Where(i => socioIds.Contains(i.SocioId) && i.UsadaUtc == null && i.RevocadaUtc == null && i.ExpiraUtc > ahora)
                .Select(i => i.SocioId)
                .Distinct()
                .ToListAsync();
            return ids.ToHashSet();
        }

        public Task SaveChangesAsync() => _context.SaveChangesAsync();
    }
}
