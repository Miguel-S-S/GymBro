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
            var sesion = await _context.Sesiones
                .Include(s => s.EjerciciosEjecutados)
                    .ThenInclude(e => e.SeriesRealizadas)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (sesion != null) await EjerciciosDeHistorial.CompletarAsync(_context, sesion.EjerciciosEjecutados);
            return sesion;
        }

        /// <summary>
        /// Correccion Etapa 8 (conteos del historial): el listado carga ejercicios y series en la misma consulta
        /// (JOIN por Include), sin una consulta adicional por sesion. Sin estos Include la coleccion quedaba vacia y
        /// el historial mostraba "0 ejercicios - 0 series". No se incluye Ejercicio (ver EjerciciosDeHistorial).
        /// </summary>
        public async Task<IEnumerable<SesionEntrenamiento>> GetAllBySocioIdAsync(Guid socioId)
        {
            return await _context.Sesiones
                .Include(s => s.EjerciciosEjecutados)
                    .ThenInclude(e => e.SeriesRealizadas)
                .Where(s => s.SocioId == socioId)
                .OrderByDescending(s => s.Inicio)
                .ToListAsync();
        }
    }
}
