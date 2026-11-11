using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Domain.Repositories;
using GymBro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GymBro.Infrastructure.Repositories
{
    public class VinculacionRepository : IVinculacionRepository
    {
        private readonly GymBroDbContext _context;
        public VinculacionRepository(GymBroDbContext context) { _context = context; }

        public Task<SolicitudVinculacion?> GetByIdAsync(Guid id) =>
            _context.SolicitudesVinculacion.FirstOrDefaultAsync(s => s.Id == id);

        public async Task<IEnumerable<SolicitudVinculacion>> GetPendientesDeGimnasioAsync(Guid gimnasioId) =>
            await _context.SolicitudesVinculacion.AsNoTracking()
                .Where(s => s.GimnasioId == gimnasioId && s.Estado == EstadoSolicitudVinculacion.Pendiente)
                .OrderBy(s => s.FechaAlta).ToListAsync();

        public async Task<IEnumerable<SolicitudVinculacion>> GetDeSocioAsync(Guid socioId) =>
            await _context.SolicitudesVinculacion.AsNoTracking()
                .Where(s => s.SocioId == socioId).OrderByDescending(s => s.FechaAlta).ToListAsync();

        public Task<bool> ExistePendienteAsync(Guid socioId, Guid gimnasioId) =>
            _context.SolicitudesVinculacion.AnyAsync(s =>
                s.SocioId == socioId && s.GimnasioId == gimnasioId && s.Estado == EstadoSolicitudVinculacion.Pendiente);

        public async Task AddAsync(SolicitudVinculacion solicitud)
        {
            await _context.SolicitudesVinculacion.AddAsync(solicitud);
            await _context.SaveChangesAsync();
        }

        public Task SaveChangesAsync() => _context.SaveChangesAsync();
    }
}