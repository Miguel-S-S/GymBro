using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Domain.Repositories;
using GymBro.Domain.Rules;
using GymBro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GymBro.Infrastructure.Repositories
{
    /// <summary>Accesos de la generación del protocolo de piernas (Etapa 7d). Sin seguimiento y sin el embedding.</summary>
    public class ProtocoloPiernasRepository : IProtocoloPiernasRepository
    {
        private readonly GymBroDbContext _context;

        public ProtocoloPiernasRepository(GymBroDbContext context)
        {
            _context = context;
        }

        public Task<Socio?> GetSocioAsync(Guid socioId) =>
            _context.Socios.AsNoTracking().FirstOrDefaultAsync(s => s.Id == socioId);

        public Task<MedicionFisica?> GetUltimaMedicionAsync(Guid socioId) =>
            _context.MedicionesFisicas.AsNoTracking()
                .Where(m => m.SocioId == socioId)
                .OrderByDescending(m => m.FechaAlta)
                .ThenBy(m => m.Id)
                .FirstOrDefaultAsync();

        public Task<bool> TienePendienteDeRevisionAsync(Guid socioId) =>
            _context.PrescripcionesEntrenamiento
                .AnyAsync(p => p.SocioId == socioId && p.Estado == EstadoPrescripcion.PendienteRevision);

        public async Task<IReadOnlyList<FilaProtocolo>> GetFilasProtocoloAsync(string protocoloCodigo, string version)
        {
            // El join con Ejercicios aplica su filtro global: un ejercicio desactivado o no visible no es candidato.
            var filas = await (
                from p in _context.EjerciciosProtocolo.AsNoTracking()
                join e in _context.Ejercicios.AsNoTracking() on p.EjercicioId equals e.Id
                where p.ProtocoloCodigo == protocoloCodigo && p.ProtocoloVersion == version
                select new FilaProtocolo(e.Id, e.Nombre, e.TipoMedida, e.TipoEquipamientoId, p.Grupo, p.Rol, p.Clase,
                    p.RodillaExcluirDesde, p.LumbarExcluirDesde, p.OrdenRotacion)
            ).ToListAsync();
            return filas.OrderBy(f => f.Grupo).ThenBy(f => f.Rol).ThenBy(f => f.OrdenRotacion).ThenBy(f => f.EjercicioId).ToList();
        }

        public async Task<IReadOnlySet<Guid>?> GetTiposEquipamientoFactiblesAsync(Guid gimnasioId)
        {
            var marcados = await _context.EquipamientosGimnasio.AsNoTracking()
                .Where(x => x.GimnasioId == gimnasioId)
                .Select(x => x.TipoEquipamientoId)
                .ToListAsync();
            if (marcados.Count == 0) return null;

            var siempre = await _context.TiposEquipamiento.AsNoTracking()
                .Where(t => t.SiempreDisponible && (t.GimnasioId == null || t.GimnasioId == gimnasioId))
                .Select(t => t.Id)
                .ToListAsync();
            return marcados.Concat(siempre).ToHashSet();
        }

        public async Task<IReadOnlyList<HistorialEjercicio>> GetUltimosRegistrosAsync(Guid socioId, IReadOnlyCollection<Guid> ejercicioIds)
        {
            var ids = ejercicioIds.Distinct().ToList();
            var ejecutados = await _context.SesionesEjercicios.AsNoTracking()
                .Where(se => ids.Contains(se.EjercicioId) && se.Sesion.SocioId == socioId)
                .Select(se => new
                {
                    se.Id, se.EjercicioId, se.Orden, se.PrescripcionEjercicioId, se.SesionId, se.Sesion.Inicio,
                    se.Sesion.PrescripcionEntrenamientoId
                })
                .ToListAsync();

            // Último registro: la sesión más reciente (inicio y luego id); si el ejercicio se repite en ella, el de menor orden.
            var ultimos = ejecutados
                .GroupBy(x => x.EjercicioId)
                .Select(g => g.OrderByDescending(x => x.Inicio).ThenByDescending(x => x.SesionId).ThenBy(x => x.Orden).First())
                .ToList();
            if (ultimos.Count == 0) return Array.Empty<HistorialEjercicio>();

            var seIds = ultimos.Select(x => x.Id).ToList();
            var series = (await _context.SeriesRealizadas.AsNoTracking()
                    .Where(s => seIds.Contains(s.SesionEjercicioId))
                    .OrderBy(s => s.OrdenSerie).ThenBy(s => s.Id)
                    .ToListAsync())
                .ToLookup(s => s.SesionEjercicioId);

            var lineasDirectas = ultimos.Where(x => x.PrescripcionEjercicioId != null).Select(x => x.PrescripcionEjercicioId!.Value).ToList();
            var rutinas = ultimos.Where(x => x.PrescripcionEjercicioId == null && x.PrescripcionEntrenamientoId != null)
                .Select(x => x.PrescripcionEntrenamientoId!.Value).Distinct().ToList();
            var lineas = await _context.PrescripcionesEjercicios.AsNoTracking()
                .Where(l => lineasDirectas.Contains(l.Id) || rutinas.Contains(l.PrescripcionEntrenamientoId))
                .ToListAsync();

            var tipos = await _context.Ejercicios.IgnoreQueryFilters().AsNoTracking()
                .Where(e => ids.Contains(e.Id))
                .ToDictionaryAsync(e => e.Id, e => e.TipoMedida);

            var resultado = new List<HistorialEjercicio>();
            foreach (var u in ultimos.OrderBy(x => x.EjercicioId))
            {
                var linea = u.PrescripcionEjercicioId is Guid id
                    ? lineas.FirstOrDefault(l => l.Id == id)
                    : u.PrescripcionEntrenamientoId is Guid rutina
                        ? lineas.Where(l => l.PrescripcionEntrenamientoId == rutina && l.EjercicioId == u.EjercicioId)
                            .OrderBy(l => l.Orden == u.Orden ? 0 : 1).ThenBy(l => l.Orden).FirstOrDefault()
                        : null;
                var tipo = tipos.TryGetValue(u.EjercicioId, out var t) ? t : TipoMedida.Repeticiones;
                resultado.Add(new HistorialEjercicio(u.EjercicioId, u.SesionId, u.Inicio,
                    series[u.Id].Select(s => new SerieEvaluada(s.PesoLevantadoKg, s.Repeticiones, s.RIR, s.EsCalentamiento)).ToList(),
                    linea is null ? null : new ObjetivoEjercicio(linea.SeriesObjetivo, linea.RepeticionesMinimas,
                        linea.RepeticionesMaximas, linea.RIRObjetivo, tipo)));
            }
            return resultado;
        }

        public async Task<IReadOnlyList<Guid>> GetAlertasVigentesAsync(Guid socioId, DateTime desdeUtc) =>
            await _context.AlertasSeguridad.AsNoTracking()
                .Where(a => a.SocioId == socioId && a.FechaBaja == null && a.Tipo == TipoAlerta.Alerta && a.FechaAlta >= desdeUtc)
                .OrderBy(a => a.FechaAlta).ThenBy(a => a.Id)
                .Select(a => a.Id)
                .ToListAsync();

        public async Task<int> GetMaxNumeroGeneracionAsync(Guid socioId, string protocoloCodigo) =>
            // Incluye las rutinas dadas de baja: los números no se reutilizan. El acceso al socio ya se validó.
            await _context.PrescripcionesEntrenamiento.IgnoreQueryFilters()
                .Where(p => p.SocioId == socioId && p.ProtocoloCodigo == protocoloCodigo)
                .MaxAsync(p => p.NumeroGeneracion) ?? 0;

        public async Task AddPrescripcionAsync(PrescripcionEntrenamiento prescripcion) =>
            await _context.PrescripcionesEntrenamiento.AddAsync(prescripcion);

        public async Task<IReadOnlyList<PrescripcionEntrenamiento>> GetPrescripcionesDeSocioAsync(Guid socioId)
        {
            var lista = await _context.PrescripcionesEntrenamiento.AsNoTracking()
                .Include(p => p.Socio)
                .Include(p => p.EjerciciosPrescritos)
                .Where(p => p.SocioId == socioId)
                .OrderByDescending(p => p.FechaAlta)
                .ToListAsync();
            await EjerciciosDeHistorial.CompletarAsync(_context, lista.SelectMany(p => p.EjerciciosPrescritos));
            return lista;
        }

        public Task SaveChangesAsync() => _context.SaveChangesAsync();
    }
}
