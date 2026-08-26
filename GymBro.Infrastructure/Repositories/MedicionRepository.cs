using GymBro.Domain.Entities;
using GymBro.Domain.Repositories;
using GymBro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GymBro.Infrastructure.Repositories
{
    public class MedicionRepository : IMedicionRepository
    {
        private readonly GymBroDbContext _context;

        public MedicionRepository(GymBroDbContext context)
        {
            _context = context;
        }

        public async Task<MedicionFisica> AddAsync(MedicionFisica medicion)
        {
            await _context.MedicionesFisicas.AddAsync(medicion);
            await _context.SaveChangesAsync();
            return medicion;
        }

        public async Task<IEnumerable<MedicionFisica>> GetBySocioIdAsync(Guid socioId)
        {
            return await _context.MedicionesFisicas
                .Where(m => m.SocioId == socioId)
                .OrderByDescending(m => m.FechaAlta)
                .ToListAsync();
        }
    }
}
