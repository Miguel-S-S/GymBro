using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Domain.Repositories;
using GymBro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GymBro.Infrastructure.Repositories
{
    /// <summary>Etapa 10a: alertas de seguridad y lectura de los datos que evalúan las reglas.</summary>
    public class AlertaSeguridadRepository : IAlertaSeguridadRepository
    {
        private readonly GymBroDbContext _context;
        public AlertaSeguridadRepository(GymBroDbContext context) { _context = context; }

        // La clave natural ignora el filtro (incluye las dadas de baja) para reactivar la misma fila: el índice único no
        // es parcial. Solo lo usa el servicio de reglas, después de haber leído el origen con los filtros de quien llama.
        public Task<AlertaSeguridad?> GetPorClaveAsync(Guid socioId, string reglaCodigo, string claveOrigen) =>
            _context.AlertasSeguridad.IgnoreQueryFilters()
                .FirstOrDefaultAsync(a => a.SocioId == socioId && a.ReglaCodigo == reglaCodigo && a.ClaveOrigen == claveOrigen);

        public async Task<IReadOnlyList<AlertaSeguridad>> GetVigentesDeOrigenesAsync(Guid socioId, string reglaCodigo,
            IReadOnlyCollection<string> clavesOrigen)
        {
            var claves = clavesOrigen.ToList();
            return await _context.AlertasSeguridad.IgnoreQueryFilters()
                .Where(a => a.SocioId == socioId && a.ReglaCodigo == reglaCodigo && a.FechaBaja == null && claves.Contains(a.ClaveOrigen))
                .ToListAsync();
        }

        public Task<AlertaSeguridad?> GetVisibleAsync(Guid id) =>
            _context.AlertasSeguridad.FirstOrDefaultAsync(a => a.Id == id);

        public async Task<IReadOnlyList<AlertaSeguridad>> ListarAsync(IReadOnlyCollection<Guid>? socios, EstadoAlerta? estado)
        {
            var q = _context.AlertasSeguridad.AsNoTracking().Include(a => a.Socio).AsQueryable();
            if (socios is not null)
            {
                var ids = socios.ToList();
                q = q.Where(a => ids.Contains(a.SocioId));
            }
            if (estado is not null) q = q.Where(a => a.Estado == estado);
            return await q.OrderByDescending(a => a.FechaAlta).ThenBy(a => a.ReglaCodigo).ToListAsync();
        }

        public async Task<SesionEntrenamiento?> GetSesionParaEvaluarAsync(Guid sesionId)
        {
            var sesion = await _context.Sesiones.AsNoTracking()
                .Include(s => s.Socio)
                .Include(s => s.EjerciciosEjecutados).ThenInclude(e => e.SeriesRealizadas)
                .Include(s => s.PrescripcionEntrenamiento).ThenInclude(p => p!.EjerciciosPrescritos)
                .AsSplitQuery()
                .FirstOrDefaultAsync(s => s.Id == sesionId);
            if (sesion is null) return null;

            // Tipo de medida y nombre del ejercicio: se leen aparte, ignorando el filtro de ejercicios (uno desactivado sigue
            // contando para la sesión ya registrada), limitado a los Id de esta sesión (mismo criterio que EjerciciosDeHistorial).
            var ids = sesion.EjerciciosEjecutados.Select(e => e.EjercicioId).Distinct().ToList();
            var ejercicios = await _context.Ejercicios.IgnoreQueryFilters().AsNoTracking()
                .Where(e => ids.Contains(e.Id))
                .Select(e => new Ejercicio { Id = e.Id, Nombre = e.Nombre, TipoMedida = e.TipoMedida })
                .ToDictionaryAsync(e => e.Id);
            foreach (var e in sesion.EjerciciosEjecutados)
                if (ejercicios.TryGetValue(e.EjercicioId, out var ej)) e.Ejercicio = ej;
            return sesion;
        }

        public Task<RegistroBienestarDiario?> GetRegistroBienestarAsync(Guid registroId) =>
            _context.RegistrosBienestarDiario.AsNoTracking().Include(r => r.Socio).FirstOrDefaultAsync(r => r.Id == registroId);

        public async Task<IReadOnlyList<RegistroBienestarDiario>> GetBienestarAsync(Guid socioId, DateOnly desde, DateOnly hasta) =>
            await _context.RegistrosBienestarDiario.AsNoTracking()
                .Where(r => r.SocioId == socioId && r.Fecha >= desde && r.Fecha <= hasta)
                .OrderBy(r => r.Fecha)
                .ToListAsync();

        public void Agregar(AlertaSeguridad alerta) => _context.AlertasSeguridad.Add(alerta);

        public void DescartarCambios() => _context.ChangeTracker.Clear();

        public Task GuardarCambiosAsync() => _context.SaveChangesAsync();
    }
}
