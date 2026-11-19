using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Domain.Repositories;
using GymBro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GymBro.Infrastructure.Repositories
{
    public class EjercicioRepository : IEjercicioRepository
    {
        private readonly GymBroDbContext _context;

        public EjercicioRepository(GymBroDbContext context)
        {
            _context = context;
        }

        public async Task<bool> ExistenTodosAsync(IEnumerable<Guid> ids)
        {
            var distinctIds = ids.Distinct().ToList();
            if (!distinctIds.Any()) return true;

            var foundCount = await _context.Ejercicios
                .Where(e => distinctIds.Contains(e.Id))
                .CountAsync();

            return foundCount == distinctIds.Count;
        }

        public async Task<(IReadOnlyList<Ejercicio> Items, int Total)> GetPaginadoAsync(
            string? texto, string? zonaObjetivo, int omitir, int tomar, OrigenEjercicio? origen = null)
        {
            var query = _context.Ejercicios.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(texto))
            {
                var patron = $"%{texto.Trim()}%";
                query = query.Where(e =>
                    EF.Functions.ILike(e.Nombre, patron) ||
                    EF.Functions.ILike(e.MusculoPrincipal, patron) ||
                    EF.Functions.ILike(e.Equipamiento, patron));
            }

            if (!string.IsNullOrWhiteSpace(zonaObjetivo))
            {
                query = query.Where(e => e.ZonaObjetivo == zonaObjetivo);
            }

            if (origen is not null)
            {
                query = query.Where(e => e.Origen == origen);
            }

            var total = await query.CountAsync();

            var items = await query
                .OrderBy(e => e.Nombre)
                .Skip(omitir)
                .Take(tomar)
                .Select(ProyeccionSinEmbedding)
                .ToListAsync();

            return (items, total);
        }

        public async Task<Ejercicio?> GetByIdAsync(Guid id)
        {
            return await _context.Ejercicios
                .AsNoTracking()
                .Where(e => e.Id == id)
                .Select(ProyeccionSinEmbedding)
                .FirstOrDefaultAsync();
        }

        public async Task<IReadOnlyList<string>> GetZonasObjetivoAsync()
        {
            return await _context.Ejercicios
                .AsNoTracking()
                .Where(e => e.ZonaObjetivo != "")
                .Select(e => e.ZonaObjetivo)
                .Distinct()
                .OrderBy(z => z)
                .ToListAsync();
        }

        public Task<Ejercicio?> GetParaEdicionAsync(Guid id) =>
            _context.Ejercicios.FirstOrDefaultAsync(e => e.Id == id);

        public async Task<IReadOnlyList<(Guid Id, string Nombre)>> GetNombresVisiblesAsync(Guid gimnasioId)
        {
            var filas = await _context.Ejercicios.AsNoTracking()
                .Where(e => e.GimnasioId == null || e.GimnasioId == gimnasioId)
                .Select(e => new { e.Id, e.Nombre })
                .ToListAsync();
            return filas.Select(f => (f.Id, f.Nombre)).ToList();
        }

        public Task<bool> EnUsoPorPrescripcionVigenteAsync(Guid ejercicioId)
        {
            var estados = new[] { EstadoPrescripcion.Aprobada, EstadoPrescripcion.Editada, EstadoPrescripcion.PendienteRevision };
            // Se ignoran los filtros de tenant: cuenta cualquier rutina vigente, aunque sea de un socio que ya no esté en el gimnasio.
            return _context.PrescripcionesEjercicios.IgnoreQueryFilters()
                .AnyAsync(pe => pe.EjercicioId == ejercicioId && pe.FechaBaja == null
                                && pe.PrescripcionEntrenamiento.FechaBaja == null
                                && estados.Contains(pe.PrescripcionEntrenamiento.Estado));
        }

        public async Task AddAsync(Ejercicio ejercicio) => await _context.Ejercicios.AddAsync(ejercicio);

        public Task SaveChangesAsync() => _context.SaveChangesAsync();

        // El vector de 768 dimensiones no se necesita para el catálogo:
        // se excluye para no transferirlo en cada consulta.
        private static readonly System.Linq.Expressions.Expression<Func<Ejercicio, Ejercicio>> ProyeccionSinEmbedding =
            e => new Ejercicio
            {
                Id = e.Id,
                Nombre = e.Nombre,
                Equipamiento = e.Equipamiento,
                ZonaObjetivo = e.ZonaObjetivo,
                MusculoPrincipal = e.MusculoPrincipal,
                Sinergistas = e.Sinergistas,
                Estabilizadores = e.Estabilizadores,
                Preparacion = e.Preparacion,
                Ejecucion = e.Ejecucion,
                Dificultad = e.Dificultad,
                TipoMedida = e.TipoMedida,
                Origen = e.Origen,
                GimnasioId = e.GimnasioId,
                TipoEquipamientoId = e.TipoEquipamientoId,
                TipoEquipamiento = e.TipoEquipamiento == null ? null
                    : new TipoEquipamiento { Id = e.TipoEquipamiento.Id, Nombre = e.TipoEquipamiento.Nombre, Clave = e.TipoEquipamiento.Clave },
                FechaAlta = e.FechaAlta
            };
    }
}
