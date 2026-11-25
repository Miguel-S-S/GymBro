using GymBro.Application.DTOs;
using GymBro.Application.Services;
using GymBro.Domain.Enums;
using GymBro.Domain.Exceptions;
using GymBro.Domain.Repositories;
using GymBro.Domain.Security;
using GymBro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace GymBro.Infrastructure.Services
{
    /// <summary>
    /// Busqueda semantica de ejercicios por similitud coseno sobre los embeddings (pgvector). Etapa 9b-1: filtros duros
    /// antes de ordenar, de modo que un ejercicio que no cumple nunca aparece aunque sea el mas parecido:
    ///   1. con embedding, activo y sin baja logica;
    ///   2. global o del gimnasio de referencia (nunca de otro gimnasio, tampoco para el Administrador de la Plataforma);
    ///   3. con equipamiento disponible en el gimnasio: si el gimnasio declaro su equipamiento, el tipo del ejercicio debe estar
    ///      marcado o ser "siempre disponible" (misma regla que el planificador de piernas,
    ///      ProtocoloPiernasRepository.GetTiposEquipamientoFactiblesAsync); si no declaro nada, no se restringe;
    ///   4. zona y claves de equipamiento opcionales del pedido.
    /// </summary>
    public class BuscadorSemanticoService : IBuscadorSemanticoService
    {
        public const int CantidadMaxima = 50;

        private readonly GymBroDbContext _context;
        private readonly IIaService _iaService;
        private readonly IUsuarioActual _actual;
        private readonly IGimnasioRepository _gimnasios;

        public BuscadorSemanticoService(GymBroDbContext context, IIaService iaService, IUsuarioActual actual, IGimnasioRepository gimnasios)
        {
            _context = context;
            _iaService = iaService;
            _actual = actual;
            _gimnasios = gimnasios;
        }

        public Task<List<EjercicioBuscadoDto>> BuscarEjerciciosSimilaresAsync(string requerimientoUsuario, int cantidad = 5) =>
            BuscarAsync(new BusquedaSemanticaRequest { Consulta = requerimientoUsuario, Cantidad = cantidad });

        public async Task<List<EjercicioBuscadoDto>> BuscarAsync(BusquedaSemanticaRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            var consulta = request.Consulta?.Trim();
            if (string.IsNullOrWhiteSpace(consulta)) throw new ReglaNegocioException("La consulta no puede estar vacía.");
            if (consulta.Length > 500) throw new ReglaNegocioException("La consulta admite hasta 500 caracteres.");
            if (request.Cantidad < 1 || request.Cantidad > CantidadMaxima)
                throw new ReglaNegocioException($"La cantidad va de 1 a {CantidadMaxima}.");

            var gimnasioId = await GimnasioDeReferenciaAsync(request.GimnasioId);

            float[] embeddingArray;
            try
            {
                embeddingArray = await _iaService.GenerarEmbeddingAsync(consulta);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                throw new ServicioExternoNoDisponibleException("El servicio de IA no está disponible en este momento. Intente más tarde.", ex);
            }
            if (embeddingArray == null || embeddingArray.Length == 0)
                throw new InvalidOperationException("El servicio de IA no devolvió un vector válido.");
            var vectorBuscado = new Vector(embeddingArray);

            var q = _context.Ejercicios.AsNoTracking()
                .Where(e => e.Embedding != null && e.Activo && e.FechaBaja == null)
                .Where(e => e.GimnasioId == null || e.GimnasioId == gimnasioId);

            if (gimnasioId is Guid g && await _context.EquipamientosGimnasio.AnyAsync(x => x.GimnasioId == g))
            {
                var marcados = _context.EquipamientosGimnasio.Where(x => x.GimnasioId == g).Select(x => x.TipoEquipamientoId);
                var siempre = _context.TiposEquipamiento
                    .Where(t => t.SiempreDisponible && (t.GimnasioId == null || t.GimnasioId == g)).Select(t => t.Id);
                q = q.Where(e => e.TipoEquipamientoId != null
                    && (marcados.Contains(e.TipoEquipamientoId.Value) || siempre.Contains(e.TipoEquipamientoId.Value)));
            }

            if (!string.IsNullOrWhiteSpace(request.Zona))
            {
                var zona = request.Zona.Trim().ToLower();
                q = q.Where(e => e.ZonaObjetivo.ToLower() == zona);
            }

            var claves = request.Equipamiento?.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim().ToUpperInvariant()).Distinct().ToList();
            if (claves is { Count: > 0 })
                q = q.Where(e => e.TipoEquipamiento != null && claves.Contains(e.TipoEquipamiento.Clave));

            return await q
                .OrderBy(e => e.Embedding!.CosineDistance(vectorBuscado))
                .ThenBy(e => e.Id)
                .Take(request.Cantidad)
                .Select(e => new EjercicioBuscadoDto
                {
                    Id = e.Id,
                    Nombre = e.Nombre,
                    ZonaObjetivo = e.ZonaObjetivo,
                    MusculoPrincipal = e.MusculoPrincipal,
                    Equipamiento = e.Equipamiento
                })
                .ToListAsync();
        }

        /// <summary>
        /// Gimnasio contra el que se filtra. Personal: el de su sesion (otro indicado responde 404, GimnasioDestino). Socio: el
        /// de su ficha (null si es independiente: solo globales). Plataforma: el indicado, validado (sin el, solo globales).
        /// </summary>
        private async Task<Guid?> GimnasioDeReferenciaAsync(Guid? indicado)
        {
            if (_actual.Rol is RolUsuario.AdministradorGimnasio or RolUsuario.Entrenador)
                return await GimnasioDestino.ResolverAsync(_actual, _gimnasios, indicado);
            if (_actual.Rol == RolUsuario.Socio && _actual.SocioId is Guid socioId)
                return await _context.Socios.Where(s => s.Id == socioId).Select(s => s.GimnasioId).FirstOrDefaultAsync();
            if (indicado is Guid g)
            {
                _ = await _gimnasios.GetByIdAsync(g) ?? throw new RecursoNoEncontradoException("Gimnasio no encontrado.");
                return g;
            }
            return null;
        }
    }
}
