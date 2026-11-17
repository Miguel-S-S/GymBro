using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Domain.Repositories;
using GymBro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GymBro.Infrastructure.Repositories
{
    public class BaselineRepository : IBaselineRepository
    {
        private readonly GymBroDbContext _context;

        public BaselineRepository(GymBroDbContext context)
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

        public async Task<MatrizRecomendacion?> BuscarMatrizAsync(
            string genero, int edad, bool hipertension, bool diabetes, CategoriaIMC categoriaImc, ObjetivoEntrenamiento objetivo)
        {
            var matriz = _context.MatricesRecomendacion.AsNoTracking();

            // 1) Coincidencia completa; entre varias filas, la de edad más cercana (desempate por id).
            var exacta = await matriz
                .Where(m => m.Genero == genero && m.AplicaHipertension == hipertension && m.AplicaDiabetes == diabetes
                            && m.CategoriaIMC == categoriaImc && m.Objetivo == objetivo)
                .OrderBy(m => Math.Abs(m.Edad - edad)).ThenBy(m => m.Id)
                .FirstOrDefaultAsync();
            if (exacta != null) return exacta;

            // 2) Fila por defecto: mantenimiento, sin patologías, IMC normal; primero del mismo género.
            var porDefecto = matriz.Where(m => !m.AplicaHipertension && !m.AplicaDiabetes
                                               && m.Objetivo == ObjetivoEntrenamiento.Mantenimiento
                                               && m.CategoriaIMC == CategoriaIMC.Normal);

            var mismoGenero = await porDefecto.Where(m => m.Genero == genero)
                .OrderBy(m => Math.Abs(m.Edad - edad)).ThenBy(m => m.Id)
                .FirstOrDefaultAsync();
            if (mismoGenero != null) return mismoGenero;

            return await porDefecto
                .OrderBy(m => Math.Abs(m.Edad - edad)).ThenBy(m => m.Id)
                .FirstOrDefaultAsync();
        }

        public Task<bool> HayDosificacionesAsync() => _context.Dosificaciones.AnyAsync();

        public async Task<DosificacionVolumetrica?> BuscarDosificacionAsync(IReadOnlyCollection<string> aliasZona)
        {
            var alias = aliasZona.Select(a => a.ToLower()).ToArray();
            // Anomalía del catálogo (verificada el 16/11/2026): las filas con parte_cuerpo = 'Abs' están cargadas
            // con zona_objetivo = 'Legs' (y alguna con series 30-60). Se excluyen para que no contaminen a Piernas.
            return await _context.Dosificaciones.AsNoTracking()
                .Where(d => alias.Contains(d.ZonaObjetivo.ToLower()) && d.ParteCuerpo.ToLower() != "abs")
                .OrderBy(d => d.ParteCuerpo).ThenBy(d => d.SubzonaObjetivo).ThenBy(d => d.Id)
                .FirstOrDefaultAsync();
        }

        public async Task<IReadOnlyList<EjercicioCandidato>> GetEjerciciosDeZonaAsync(IReadOnlyCollection<string> aliasZona)
        {
            var alias = aliasZona.Select(a => a.ToLower()).ToArray();
            // Se proyecta sin el embedding (vector de 768 dimensiones).
            return await _context.Ejercicios.AsNoTracking()
                .Where(e => alias.Contains(e.ZonaObjetivo.ToLower()))
                .OrderBy(e => e.Dificultad).ThenBy(e => e.Nombre).ThenBy(e => e.Id)
                .Select(e => new EjercicioCandidato(e.Id, e.Nombre, e.Equipamiento, e.ZonaObjetivo, e.MusculoPrincipal, e.Dificultad))
                .ToListAsync();
        }

        public Task<bool> TienePendienteDeRevisionAsync(Guid socioId) =>
            _context.PrescripcionesEntrenamiento
                .AnyAsync(p => p.SocioId == socioId && p.Estado == EstadoPrescripcion.PendienteRevision);

        public async Task AddPrescripcionAsync(PrescripcionEntrenamiento prescripcion) =>
            await _context.PrescripcionesEntrenamiento.AddAsync(prescripcion);

        public async Task<IReadOnlyList<PrescripcionEntrenamiento>> GetPrescripcionesDeSocioAsync(Guid socioId) =>
            await _context.PrescripcionesEntrenamiento.AsNoTracking()
                .Include(p => p.Socio)
                .Include(p => p.EjerciciosPrescritos).ThenInclude(pe => pe.Ejercicio)
                .Where(p => p.SocioId == socioId)
                .OrderByDescending(p => p.FechaAlta)
                .ToListAsync();

        public Task SaveChangesAsync() => _context.SaveChangesAsync();
    }
}
