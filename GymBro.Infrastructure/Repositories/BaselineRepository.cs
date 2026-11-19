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

        public async Task<DosificacionVolumetrica?> BuscarDosificacionAsync(IReadOnlyCollection<string> aliasZona, TipoMedida tipoMedida)
        {
            var alias = aliasZona.Select(a => a.ToLower()).ToArray();
            // Etapa 9a-1: ya no se excluyen las filas 'abs'. La migración Etapa9a1_CatalogoCore las pasó a la zona Core
            // (antes figuraban en Legs) y dio de baja la fila 30-60 x 1; excluirlas ahora dejaría a Core sin su dosis.
            return await _context.Dosificaciones.AsNoTracking()
                .Where(d => alias.Contains(d.ZonaObjetivo.ToLower()) && d.TipoMedida == tipoMedida)
                .OrderBy(d => d.ParteCuerpo).ThenBy(d => d.SubzonaObjetivo).ThenBy(d => d.Id)
                .FirstOrDefaultAsync();
        }

        public async Task<IReadOnlyList<EjercicioCandidato>> GetEjerciciosDeZonaAsync(IReadOnlyCollection<string> aliasZona, Guid? gimnasioId)
        {
            var alias = aliasZona.Select(a => a.ToLower()).ToArray();
            // Se proyecta sin el embedding (vector de 768 dimensiones).
            // Etapa 9a-2: además del filtro de EF, se limita explícitamente a los globales y a los del gimnasio del socio.
            return await _context.Ejercicios.AsNoTracking()
                .Where(e => alias.Contains(e.ZonaObjetivo.ToLower()))
                .Where(e => e.GimnasioId == null || (gimnasioId != null && e.GimnasioId == gimnasioId))
                .OrderBy(e => e.Dificultad).ThenBy(e => e.Nombre).ThenBy(e => e.Id)
                .Select(e => new EjercicioCandidato(e.Id, e.Nombre, e.Equipamiento, e.ZonaObjetivo, e.MusculoPrincipal, e.Dificultad,
                    e.TipoMedida, e.TipoEquipamientoId))
                .ToListAsync();
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

        public Task<bool> TienePendienteDeRevisionAsync(Guid socioId) =>
            _context.PrescripcionesEntrenamiento
                .AnyAsync(p => p.SocioId == socioId && p.Estado == EstadoPrescripcion.PendienteRevision);

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
            // Etapa 9a-2: el nombre del ejercicio no se pierde aunque el filtro por gimnasio lo oculte.
            await EjerciciosDeHistorial.CompletarAsync(_context, lista.SelectMany(p => p.EjerciciosPrescritos));
            return lista;
        }

        public Task SaveChangesAsync() => _context.SaveChangesAsync();
    }
}
