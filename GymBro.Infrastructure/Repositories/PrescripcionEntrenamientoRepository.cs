using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Domain.Repositories;
using GymBro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GymBro.Infrastructure.Repositories
{
    public class PrescripcionEntrenamientoRepository : IPrescripcionEntrenamientoRepository
    {
        private readonly GymBroDbContext _context;

        public PrescripcionEntrenamientoRepository(GymBroDbContext context)
        {
            _context = context;
        }

        public async Task<bool> ExistsAsync(Guid id)
        {
            return await _context.PrescripcionesEntrenamiento.AnyAsync(p => p.Id == id);
        }

        public async Task<IEnumerable<PrescripcionEntrenamiento>> GetPendientesDeRevisionAsync()
        {
            return await _context.PrescripcionesEntrenamiento.AsNoTracking()
                .Include(p => p.Socio)
                .Include(p => p.EjerciciosPrescritos).ThenInclude(pe => pe.Ejercicio)
                .Where(p => p.Estado == EstadoPrescripcion.PendienteRevision)
                .OrderBy(p => p.FechaAlta)
                .ToListAsync();
        }

        public async Task<PrescripcionEntrenamiento?> GetForRevisionAsync(Guid id)
        {
            return await _context.PrescripcionesEntrenamiento
                .Include(p => p.Socio)
                .Include(p => p.EjerciciosPrescritos).ThenInclude(pe => pe.Ejercicio)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task AddRevisionAsync(RevisionPrescripcion revision)
        {
            await _context.RevisionesPrescripcion.AddAsync(revision);
        }

        public Task SaveChangesAsync() => _context.SaveChangesAsync();

        public async Task<PrescripcionEntrenamiento?> GetByIdAsync(Guid id)
        {
            return await _context.PrescripcionesEntrenamiento
                .Include(p => p.EjerciciosPrescritos)
                    .ThenInclude(pe => pe.Ejercicio)
                .FirstOrDefaultAsync(p => p.Id == id);
        }
    }
}