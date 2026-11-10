using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GymBro.Domain.Entities;
using GymBro.Domain.Repositories;
using GymBro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GymBro.Infrastructure.Repositories
{
    public class SesionEntrenamientoRepository : ISesionEntrenamientoRepository
    {
        private readonly GymBroDbContext _context;

        public SesionEntrenamientoRepository(GymBroDbContext context)
        {
            _context = context;
        }

        public async Task<SesionEntrenamiento> AddAsync(SesionEntrenamiento sesion)
        {
            await _context.Sesiones.AddAsync(sesion);
            await _context.SaveChangesAsync();
            return sesion;
        }

        public async Task<SesionEntrenamiento?> GetByIdAsync(Guid id)
        {
            return await _context.Sesiones
                .Include(s => s.EjerciciosEjecutados)
                    .ThenInclude(e => e.Ejercicio)
                .Include(s => s.EjerciciosEjecutados)
                    .ThenInclude(e => e.SeriesRealizadas)
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<IEnumerable<SesionEntrenamiento>> GetAllBySocioIdAsync(Guid socioId)
        {
            return await _context.Sesiones
                .Where(s => s.SocioId == socioId)
                .OrderByDescending(s => s.Inicio)
                .ToListAsync();
        }
    }
}
