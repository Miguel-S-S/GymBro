using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Domain.Repositories;
using GymBro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GymBro.Infrastructure.Repositories
{
    public class AuditLogRepository : IAuditLogRepository
    {
        private readonly GymBroDbContext _context;
        public AuditLogRepository(GymBroDbContext context) { _context = context; }

        public async Task AddAsync(AuditLog registro)
        {
            await _context.AuditLogs.AddAsync(registro);
            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<AuditLog>> GetRecientesAsync(int cantidad, Guid? gimnasioId) =>
            await _context.AuditLogs.AsNoTracking()
                .Where(a => gimnasioId == null || a.GimnasioId == gimnasioId)
                .OrderByDescending(a => a.Fecha).Take(cantidad).ToListAsync();
    }
}