using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Domain.Repositories;
using GymBro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GymBro.Infrastructure.Repositories
{
    /// <summary>
    /// Etapa 11: nombres para los listados de gestion. Las filas de asistencia y cuota ya vienen filtradas por gimnasio;
    /// el nombre del socio se proyecta sin el filtro de socios (que excluye al Administrador de la Plataforma porque la
    /// ficha tiene datos de salud) y solo con nombre y apellido: ningun dato de salud sale de esta consulta.
    /// </summary>
    internal static class NombresGestion
    {
        public static async Task<(Dictionary<Guid, string> Socios, Dictionary<Guid, string> Gimnasios)> CargarAsync(
            GymBroDbContext context, IEnumerable<Guid> socioIds, IEnumerable<Guid> gimnasioIds)
        {
            var ids = socioIds.Distinct().ToList();
            var gids = gimnasioIds.Distinct().ToList();
            var socios = await context.Socios.IgnoreQueryFilters().AsNoTracking()
                .Where(s => ids.Contains(s.Id))
                .Select(s => new { s.Id, s.Nombre, s.Apellido })
                .ToDictionaryAsync(s => s.Id, s => $"{s.Nombre} {s.Apellido}");
            var gimnasios = await context.Gimnasios.IgnoreQueryFilters().AsNoTracking()
                .Where(g => gids.Contains(g.Id))
                .Select(g => new { g.Id, g.Nombre })
                .ToDictionaryAsync(g => g.Id, g => g.Nombre);
            return (socios, gimnasios);
        }

        public static bool EsDuplicado(DbUpdateException ex) =>
            ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
    }

    public class AsistenciaRepository : IAsistenciaRepository
    {
        private readonly GymBroDbContext _context;
        public AsistenciaRepository(GymBroDbContext context) { _context = context; }

        public Task<Asistencia?> GetDelDiaAsync(Guid socioId, DateOnly fecha) =>
            _context.Asistencias.AsNoTracking().FirstOrDefaultAsync(a => a.SocioId == socioId && a.Fecha == fecha);

        public async Task<IReadOnlyList<FilaAsistencia>> ListarAsync(DateOnly fecha, Guid? gimnasioId)
        {
            var consulta = _context.Asistencias.AsNoTracking().Where(a => a.Fecha == fecha);
            if (gimnasioId is Guid g) consulta = consulta.Where(a => a.GimnasioId == g);
            var filas = await consulta.OrderByDescending(a => a.Instante).ToListAsync();
            var (socios, gimnasios) = await NombresGestion.CargarAsync(_context, filas.Select(f => f.SocioId), filas.Select(f => f.GimnasioId));
            return filas.Select(a => new FilaAsistencia(a, socios.GetValueOrDefault(a.SocioId, string.Empty),
                gimnasios.GetValueOrDefault(a.GimnasioId, string.Empty))).ToList();
        }

        public void Agregar(Asistencia asistencia) => _context.Asistencias.Add(asistencia);

        public void Descartar(Asistencia asistencia) => _context.Entry(asistencia).State = EntityState.Detached;

        public async Task<bool> GuardarCambiosAsync()
        {
            try
            {
                await _context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateException ex) when (NombresGestion.EsDuplicado(ex))
            {
                return false;
            }
        }
    }

    public class CuotaRepository : ICuotaRepository
    {
        private readonly GymBroDbContext _context;
        public CuotaRepository(GymBroDbContext context) { _context = context; }

        public Task<CuotaSocio?> GetForUpdateAsync(Guid id) => _context.CuotasSocio.FirstOrDefaultAsync(c => c.Id == id);

        public async Task<HashSet<Guid>> SociosConCuotaAsync(Guid gimnasioId, int anio, int mes) =>
            (await _context.CuotasSocio.AsNoTracking()
                .Where(c => c.GimnasioId == gimnasioId && c.Anio == anio && c.Mes == mes)
                .Select(c => c.SocioId).ToListAsync()).ToHashSet();

        public Task<IReadOnlyList<FilaCuota>> ListarPeriodoAsync(int anio, int mes, Guid? gimnasioId) =>
            ListarAsync(_context.CuotasSocio.AsNoTracking().Where(c => c.Anio == anio && c.Mes == mes), gimnasioId);

        public Task<IReadOnlyList<FilaCuota>> ListarVencidasAsync(DateOnly hoy, Guid? gimnasioId) =>
            ListarAsync(_context.CuotasSocio.AsNoTracking().Where(c => c.Estado == EstadoCuota.Pendiente && c.Vencimiento < hoy), gimnasioId);

        public async Task<IReadOnlyList<CuotaSocio>> ListarDelSocioAsync(Guid socioId) =>
            await _context.CuotasSocio.AsNoTracking().Where(c => c.SocioId == socioId)
                .OrderByDescending(c => c.Anio).ThenByDescending(c => c.Mes).ToListAsync();

        public void AgregarRango(IEnumerable<CuotaSocio> cuotas) => _context.CuotasSocio.AddRange(cuotas);

        public async Task<bool> GuardarCambiosAsync()
        {
            try
            {
                await _context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateException ex) when (NombresGestion.EsDuplicado(ex))
            {
                return false;
            }
        }

        private async Task<IReadOnlyList<FilaCuota>> ListarAsync(IQueryable<CuotaSocio> consulta, Guid? gimnasioId)
        {
            if (gimnasioId is Guid g) consulta = consulta.Where(c => c.GimnasioId == g);
            var filas = await consulta.OrderBy(c => c.Vencimiento).ThenBy(c => c.SocioId).ToListAsync();
            var (socios, gimnasios) = await NombresGestion.CargarAsync(_context, filas.Select(f => f.SocioId), filas.Select(f => f.GimnasioId));
            return filas.Select(c => new FilaCuota(c, socios.GetValueOrDefault(c.SocioId, string.Empty),
                gimnasios.GetValueOrDefault(c.GimnasioId, string.Empty)))
                .OrderBy(f => f.SocioNombre, StringComparer.CurrentCultureIgnoreCase).ToList();
        }
    }
}
