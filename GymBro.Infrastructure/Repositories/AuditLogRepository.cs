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

        public async Task<IEnumerable<AuditLog>> GetRecientesAsync(int cantidad, Guid? gimnasioId,
            DateTime? desdeUtc = null, DateTime? hastaUtc = null, string? accion = null, string? entidad = null)
        {
            var consulta = _context.AuditLogs.AsNoTracking().AsQueryable();
            if (gimnasioId != null) consulta = consulta.Where(a => a.GimnasioId == gimnasioId);
            if (desdeUtc != null) consulta = consulta.Where(a => a.Fecha >= desdeUtc);
            if (hastaUtc != null) consulta = consulta.Where(a => a.Fecha < hastaUtc);
            if (!string.IsNullOrWhiteSpace(accion)) consulta = consulta.Where(a => a.Accion == accion);
            if (!string.IsNullOrWhiteSpace(entidad)) consulta = consulta.Where(a => a.Entidad == entidad);
            return await consulta.OrderByDescending(a => a.Fecha).Take(cantidad).ToListAsync();
        }
    }
}