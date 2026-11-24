using GymBro.Application.DTOs;
using GymBro.Domain.Enums;
using GymBro.Domain.Exceptions;
using GymBro.Domain.Repositories;

namespace GymBro.Application.Services
{
    public interface ILimitacionesArticularesService
    {
        Task<LimitacionesArticularesDto> ObtenerAsync(Guid socioId);

        /// <summary>Actualiza las limitaciones de rodilla y lumbar. Exige el consentimiento para datos de salud.</summary>
        Task<LimitacionesArticularesDto> ActualizarAsync(Guid socioId, LimitacionesArticularesDto request);
    }

    /// <summary>
    /// Limitaciones articulares del socio (Etapa 7d, decisión D2). Son datos de salud: mismas reglas de acceso que las
    /// mediciones (Dueño de su gimnasio, Personal Trainer asignado y el propio Socio; el Administrador de la Plataforma no
    /// accede) y el cambio exige el consentimiento. La auditoría no guarda los valores (la consulta el Administrador de la
    /// Plataforma).
    /// </summary>
    public class LimitacionesArticularesService : ILimitacionesArticularesService
    {
        private readonly ISocioRepository _socios;
        private readonly ISocioAccessService _acceso;
        private readonly IAuditService _audit;
        private readonly TimeProvider _reloj;

        public LimitacionesArticularesService(ISocioRepository socios, ISocioAccessService acceso, IAuditService audit, TimeProvider reloj)
        {
            _socios = socios;
            _acceso = acceso;
            _audit = audit;
            _reloj = reloj;
        }

        public async Task<LimitacionesArticularesDto> ObtenerAsync(Guid socioId)
        {
            await _acceso.AsegurarAccesoAsync(socioId);
            var socio = await _socios.GetByIdAsync(socioId) ?? throw new RecursoNoEncontradoException("Socio no encontrado.");
            return new LimitacionesArticularesDto { Rodilla = socio.LimitacionRodilla.ToString(), Lumbar = socio.LimitacionLumbar.ToString() };
        }

        public async Task<LimitacionesArticularesDto> ActualizarAsync(Guid socioId, LimitacionesArticularesDto request)
        {
            await _acceso.AsegurarAccesoAsync(socioId);
            var socio = await _socios.GetForUpdateAsync(socioId) ?? throw new RecursoNoEncontradoException("Socio no encontrado.");
            if (socio.ConsentimientoDatosSaludFecha is null)
                throw new ReglaNegocioException("El socio todavía no otorgó el consentimiento para el tratamiento de sus datos de salud.");

            var rodilla = LeerNivel(request.Rodilla, "rodilla");
            var lumbar = LeerNivel(request.Lumbar, "zona lumbar");
            if (socio.LimitacionRodilla != rodilla || socio.LimitacionLumbar != lumbar)
            {
                socio.LimitacionRodilla = rodilla;
                socio.LimitacionLumbar = lumbar;
                socio.FechaModificacion = _reloj.GetUtcNow().UtcDateTime;
                await _socios.SaveChangesAsync();
            }
            await _audit.RegistrarAsync("LIMITACIONES_ARTICULARES", "Socio", socioId.ToString());
            return new LimitacionesArticularesDto { Rodilla = rodilla.ToString(), Lumbar = lumbar.ToString() };
        }

        /// <summary>Solo admite los nombres del enum (sin distinguir mayúsculas); no acepta números ni listas.</summary>
        public static NivelLimitacion LeerNivel(string? texto, string articulacion)
        {
            var valor = (texto ?? string.Empty).Trim();
            foreach (var n in Enum.GetValues<NivelLimitacion>())
                if (string.Equals(n.ToString(), valor, StringComparison.OrdinalIgnoreCase))
                    return n;
            throw new ReglaNegocioException($"Limitación de {articulacion} inválida. Use SinLimitacion, Moderada o Alta.");
        }
    }
}
