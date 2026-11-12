using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Domain.Repositories;
using GymBro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace GymBro.Infrastructure.Repositories
{
    public class GimnasioRepository : IGimnasioRepository
    {
        private readonly GymBroDbContext _context;
        public GimnasioRepository(GymBroDbContext context) { _context = context; }

        public Task<Gimnasio?> GetByIdAsync(Guid id) =>
            _context.Gimnasios.FirstOrDefaultAsync(g => g.Id == id);

        // El socio todavía no pertenece al gimnasio: el código se resuelve sin filtro de tenant.
        public Task<Gimnasio?> GetByCodigoAsync(string codigo) =>
            _context.Gimnasios.IgnoreQueryFilters()
                .FirstOrDefaultAsync(g => g.Codigo == codigo && g.FechaBaja == null);

        public Task<bool> ExisteCodigoAsync(string codigo) =>
            _context.Gimnasios.IgnoreQueryFilters().AnyAsync(g => g.Codigo == codigo);

        public async Task<IEnumerable<Gimnasio>> GetAllAsync() =>
            await _context.Gimnasios.AsNoTracking().OrderBy(g => g.Nombre).ToListAsync();

        public async Task AddAsync(Gimnasio gimnasio, Usuario dueno)
        {
            await _context.Gimnasios.AddAsync(gimnasio);
            await _context.Usuarios.AddAsync(dueno);
            await _context.SaveChangesAsync(); // gimnasio  dueño: todo o nada
        }

        public Task<bool> RequiereAprobacionAsync(Guid gimnasioId) =>
            _context.Gimnasios.IgnoreQueryFilters()
                .Where(g => g.Id == gimnasioId).Select(g => g.RequiereAprobacionRutinas).FirstOrDefaultAsync();

        public Task<bool> EstaActivoAsync(Guid gimnasioId) =>
            _context.Gimnasios.IgnoreQueryFilters()
                .AnyAsync(g => g.Id == gimnasioId && g.Activo && g.FechaBaja == null);

        public Task SaveChangesAsync() => _context.SaveChangesAsync();
    }
}