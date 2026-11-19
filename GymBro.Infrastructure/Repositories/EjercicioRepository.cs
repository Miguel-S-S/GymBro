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
            string? texto, string? zonaObjetivo, int omitir, int tomar, OrigenEjercicio? origen = null,
            AlcanceInactivos? inactivos = null)
        {
            var query = inactivos is null ? _context.Ejercicios.AsNoTracking() : Inactivos(inactivos);

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
            await CompletarAutoresAsync(items);

            return (items, total);
        }

        public async Task<Ejercicio?> GetByIdAsync(Guid id, AlcanceInactivos? inactivos = null)
        {
            var activo = await _context.Ejercicios
                .AsNoTracking()
                .Where(e => e.Id == id)
                .Select(ProyeccionSinEmbedding)
                .FirstOrDefaultAsync();
            var ejercicio = activo ?? (inactivos is null ? null
                : await Inactivos(inactivos).Where(e => e.Id == id).Select(ProyeccionSinEmbedding).FirstOrDefaultAsync());
            if (ejercicio is not null) await CompletarAutoresAsync(new[] { ejercicio });
            return ejercicio;
        }

        /// <summary>
        /// Etapa 9a-2b: autor de los ejercicios propios ("Propuesto por"). Se lee aparte, ignorando el filtro de usuarios,
        /// solo para los Id que ya referencian ejercicios visibles, y solo nombre y rol (sin email ni otros datos). Así el
        /// resultado no depende de si el autor es del gimnasio o es el Administrador de la Plataforma. Quién puede ver el
        /// nombre lo decide el servicio.
        /// </summary>
        private async Task CompletarAutoresAsync(IReadOnlyCollection<Ejercicio> items)
        {
            var ids = items.Where(e => e.CreadoPorUsuarioId != null).Select(e => e.CreadoPorUsuarioId!.Value).Distinct().ToList();
            if (ids.Count == 0) return;
            var autores = await _context.Usuarios.IgnoreQueryFilters().AsNoTracking()
                .Where(u => ids.Contains(u.Id))
                .Select(u => new Usuario { Id = u.Id, NombreCompleto = u.NombreCompleto, Rol = u.Rol })
                .ToDictionaryAsync(u => u.Id);
            foreach (var e in items)
                if (e.CreadoPorUsuarioId is { } autor && autores.TryGetValue(autor, out var u)) e.CreadoPor = u;
        }

        /// <summary>
        /// Etapa 9a-2b: ejercicios desactivados (no dados de baja) del alcance indicado. Ignora el filtro global, así que
        /// la regla de visibilidad se aplica acá de forma explícita: todos, o solo los propios de ese gimnasio.
        /// </summary>
        private IQueryable<Ejercicio> Inactivos(AlcanceInactivos alcance) =>
            _context.Ejercicios.IgnoreQueryFilters().AsNoTracking()
                .Where(e => e.FechaBaja == null && !e.Activo
                            && (alcance.Todos || (alcance.GimnasioId != null && e.GimnasioId == alcance.GimnasioId)));

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

        public Task<Ejercicio?> GetParaGestionAsync(Guid id) =>
            _context.Ejercicios.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.Id == id && e.FechaBaja == null);

        public async Task<IReadOnlyList<(Guid Id, string Nombre)>> GetNombresVisiblesAsync(Guid gimnasioId)
        {
            // Etapa 9a-2b: incluye los desactivados, para que reactivar uno nunca produzca dos con el mismo nombre.
            var filas = await _context.Ejercicios.IgnoreQueryFilters().AsNoTracking()
                .Where(e => e.FechaBaja == null && (e.GimnasioId == null || e.GimnasioId == gimnasioId))
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

        public async Task<IReadOnlyList<string>> GetPropiosActivosPorTiposAsync(Guid gimnasioId, IReadOnlyCollection<Guid> tipos)
        {
            if (tipos.Count == 0) return Array.Empty<string>();
            var ids = tipos.ToList();
            return await _context.Ejercicios.IgnoreQueryFilters().AsNoTracking()
                .Where(e => e.FechaBaja == null && e.Activo && e.GimnasioId == gimnasioId
                            && e.TipoEquipamientoId != null && ids.Contains(e.TipoEquipamientoId.Value))
                .OrderBy(e => e.Nombre)
                .Select(e => e.Nombre)
                .ToListAsync();
        }

        public Task<int> ContarPorTipoAsync(Guid tipoEquipamientoId) =>
            _context.Ejercicios.IgnoreQueryFilters()
                .CountAsync(e => e.FechaBaja == null && e.TipoEquipamientoId == tipoEquipamientoId);

        public Task ActualizarTextoEquipamientoAsync(Guid tipoEquipamientoId, string nombre) =>
            _context.Ejercicios.IgnoreQueryFilters()
                .Where(e => e.TipoEquipamientoId == tipoEquipamientoId && e.Origen == OrigenEjercicio.Gimnasio)
                .ExecuteUpdateAsync(x => x.SetProperty(e => e.Equipamiento, nombre));

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
                Activo = e.Activo,
                CreadoPorUsuarioId = e.CreadoPorUsuarioId,
                FechaAlta = e.FechaAlta
            };
    }
}
