using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Domain.Repositories;
using GymBro.Domain.Rules;
using GymBro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GymBro.Infrastructure.Repositories
{
    /// <summary>Lecturas de los KPIs de progresión (Etapa 7d).</summary>
    public class KpiProgresionRepository : IKpiProgresionRepository
    {
        private readonly GymBroDbContext _context;

        public KpiProgresionRepository(GymBroDbContext context)
        {
            _context = context;
        }

        public Task<Socio?> GetSocioAsync(Guid socioId) =>
            _context.Socios.AsNoTracking().FirstOrDefaultAsync(s => s.Id == socioId);

        public async Task<IReadOnlyList<SesionEntrenamiento>> GetSesionesAsync(Guid socioId, DateTime? desdeUtc, DateTime? hastaUtc)
        {
            var q = _context.Sesiones.AsNoTracking()
                .Include(s => s.EjerciciosEjecutados).ThenInclude(e => e.SeriesRealizadas)
                .Include(s => s.PrescripcionEntrenamiento).ThenInclude(p => p!.EjerciciosPrescritos)
                .AsSplitQuery()
                .Where(s => s.SocioId == socioId);
            if (desdeUtc is DateTime d) q = q.Where(s => s.Inicio >= d);
            if (hastaUtc is DateTime h) q = q.Where(s => s.Inicio <= h);
            var sesiones = await q.OrderBy(s => s.Inicio).ThenBy(s => s.Id).ToListAsync();

            var ids = sesiones.SelectMany(s => s.EjerciciosEjecutados).Select(e => e.EjercicioId).Distinct().ToList();
            var ejercicios = await _context.Ejercicios.IgnoreQueryFilters().AsNoTracking()
                .Where(e => ids.Contains(e.Id))
                .Select(e => new Ejercicio { Id = e.Id, Nombre = e.Nombre, TipoMedida = e.TipoMedida })
                .ToDictionaryAsync(e => e.Id);
            foreach (var e in sesiones.SelectMany(s => s.EjerciciosEjecutados))
                if (ejercicios.TryGetValue(e.EjercicioId, out var ej)) e.Ejercicio = ej;
            return sesiones;
        }

        public async Task<IReadOnlyDictionary<Guid, GrupoMuscularPiernas>> GetGruposProtocoloAsync(string protocoloCodigo)
        {
            var filas = await _context.EjerciciosProtocolo.AsNoTracking()
                .Where(p => p.ProtocoloCodigo == protocoloCodigo)
                .Select(p => new { p.EjercicioId, p.Grupo, p.ProtocoloVersion })
                .ToListAsync();
            return filas.GroupBy(f => f.EjercicioId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(f => f.ProtocoloVersion, StringComparer.Ordinal).First().Grupo);
        }

        public async Task<IReadOnlyList<AlertaSeguridad>> GetAlertasVigentesAsync(Guid socioId) =>
            await _context.AlertasSeguridad.AsNoTracking()
                .Where(a => a.SocioId == socioId && a.FechaBaja == null)
                .OrderBy(a => a.FechaAlta).ThenBy(a => a.Id)
                .ToListAsync();

        public async Task<IReadOnlyDictionary<Guid, DateOnly>> GetFechasBienestarAsync(Guid socioId) =>
            await _context.RegistrosBienestarDiario.AsNoTracking()
                .Where(r => r.SocioId == socioId)
                .ToDictionaryAsync(r => r.Id, r => r.Fecha);

        public async Task<IReadOnlyList<PrescripcionEntrenamiento>> GetPrescripcionesProtocoloAsync(Guid socioId, string protocoloCodigo,
            DateTime? desdeUtc, DateTime? hastaUtc)
        {
            var q = _context.PrescripcionesEntrenamiento.AsNoTracking()
                .Include(p => p.EjerciciosPrescritos)
                .Where(p => p.SocioId == socioId && p.ProtocoloCodigo == protocoloCodigo);
            if (desdeUtc is DateTime d) q = q.Where(p => p.FechaAlta >= d);
            if (hastaUtc is DateTime h) q = q.Where(p => p.FechaAlta <= h);
            return await q.OrderBy(p => p.FechaAlta).ThenBy(p => p.Id).ToListAsync();
        }
    }
}
