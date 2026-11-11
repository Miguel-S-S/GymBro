using GymBro.Domain.Entities;
using GymBro.Domain.Repositories;
using GymBro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GymBro.Infrastructure.Repositories
{
    public class SocioRepository : ISocioRepository
    {
        private readonly GymBroDbContext _context;

        public SocioRepository(GymBroDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Socio>> GetAllAsync()
        {
            return await _context.Socios.ToListAsync();
        }

        public async Task<Socio?> GetByIdAsync(Guid id)
        {
            return await _context.Socios
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<Socio> AddAsync(Socio socio)
        {
            await _context.Socios.AddAsync(socio);
            await _context.SaveChangesAsync();
            return socio;
        }

        public async Task<bool> ExistsAsync(Guid id)
        {
            return await _context.Socios.AnyAsync(s => s.Id == id);
        }

        public async Task<IEnumerable<Socio>> GetByEntrenadorAsync(Guid entrenadorUsuarioId)
        {
            return await _context.Socios.AsNoTracking()
                .Where(s => _context.Asignaciones.Any(a => a.SocioId == s.Id && a.EntrenadorUsuarioId == entrenadorUsuarioId))
                .OrderBy(s => s.Apellido).ThenBy(s => s.Nombre)
                .ToListAsync();
        }

        // El DNI es único en toda la plataforma, por eso se consulta sin filtro de tenant.
        public Task<bool> ExisteDniAsync(string dni) =>
            _context.Socios.IgnoreQueryFilters().AnyAsync(s => s.Dni == dni && s.FechaBaja == null);

        // Se usa al resolver una vinculación: el socio todavía no es del gimnasio. La autorización
        // se verifica antes, a través de la solicitud (que sí está filtrada por gimnasio).
        public Task<Socio?> GetForUpdateAsync(Guid id) =>
            _context.Socios.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == id && s.FechaBaja == null);

        public Task SaveChangesAsync() => _context.SaveChangesAsync();
    }
}