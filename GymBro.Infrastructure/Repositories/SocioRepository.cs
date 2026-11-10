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
    }
}
