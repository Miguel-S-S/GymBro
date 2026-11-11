using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Domain.Repositories;
using GymBro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GymBro.Infrastructure.Repositories
{
    public class AsignacionRepository : IAsignacionRepository
    {
        private readonly GymBroDbContext _context;
        public AsignacionRepository(GymBroDbContext context) { _context = context; }

        public Task<bool> EstaAsignadoAsync(Guid entrenadorUsuarioId, Guid socioId) =>
            _context.Asignaciones.AnyAsync(a => a.EntrenadorUsuarioId == entrenadorUsuarioId && a.SocioId == socioId);

        public Task<bool> ExisteAsync(Guid entrenadorUsuarioId, Guid socioId) =>
            EstaAsignadoAsync(entrenadorUsuarioId, socioId);

        public async Task<IEnumerable<Guid>> GetSocioIdsAsync(Guid entrenadorUsuarioId) =>
            await _context.Asignaciones.Where(a => a.EntrenadorUsuarioId == entrenadorUsuarioId)
                .Select(a => a.SocioId).ToListAsync();

        public async Task AddAsync(AsignacionEntrenadorSocio asignacion)
        {
            await _context.Asignaciones.AddAsync(asignacion);
            await _context.SaveChangesAsync();
        }
    }
}