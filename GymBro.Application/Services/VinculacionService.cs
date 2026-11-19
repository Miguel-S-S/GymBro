using GymBro.Application.DTOs.Acceso;
using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Domain.Exceptions;
using GymBro.Domain.Repositories;
using GymBro.Domain.Security;

namespace GymBro.Application.Services
{
    public interface IVinculacionService
    {
        Task<SolicitudVinculacionDto> SolicitarAsync(string codigoGimnasio);
        Task<IEnumerable<SolicitudVinculacionDto>> ListarMisSolicitudesAsync();
        /// <summary>Etapa 9a-2c: el Administrador de la Plataforma indica el gimnasio; el Dueño opera sobre el suyo.</summary>
        Task<IEnumerable<SolicitudVinculacionDto>> ListarPendientesAsync(Guid? gimnasioId = null);
        Task<SolicitudVinculacionDto> ResolverAsync(Guid solicitudId, bool aprobar, string? motivo, Guid? gimnasioId = null);
    }

    /// <summary>Vinculación socio–gimnasio con validación cruzada por DNI (Anexo II, objetivo específico 2).</summary>
    public class VinculacionService : IVinculacionService
    {
        private readonly IVinculacionRepository _solicitudes;
        private readonly ISocioRepository _socios;
        private readonly IGimnasioRepository _gimnasios;
        private readonly IUsuarioActual _actual;
        private readonly IAuditService _audit;

        public VinculacionService(IVinculacionRepository solicitudes, ISocioRepository socios, IGimnasioRepository gimnasios,
            IUsuarioActual actual, IAuditService audit)
        {
            _solicitudes = solicitudes;
            _socios = socios;
            _gimnasios = gimnasios;
            _actual = actual;
            _audit = audit;
        }

        public async Task<SolicitudVinculacionDto> SolicitarAsync(string codigoGimnasio)
        {
            var socioId = _actual.SocioId ?? throw new AccesoDenegadoException();
            var socio = await _socios.GetByIdAsync(socioId) ?? throw new RecursoNoEncontradoException("Socio no encontrado.");

            if (socio.GimnasioId is not null)
                throw new ReglaNegocioException("Ya estás vinculado a un gimnasio.");
            if (string.IsNullOrWhiteSpace(socio.Dni))
                throw new ReglaNegocioException("Tu perfil no tiene DNI cargado.");

            var gimnasio = await _gimnasios.GetByCodigoAsync((codigoGimnasio ?? string.Empty).Trim().ToUpperInvariant());
            if (gimnasio is null || !gimnasio.Activo)
                throw new RecursoNoEncontradoException("No existe un gimnasio con ese código.");
            if (await _solicitudes.ExistePendienteAsync(socioId, gimnasio.Id))
                throw new ReglaNegocioException("Ya tenés una solicitud pendiente en este gimnasio.");

            var solicitud = new SolicitudVinculacion
            {
                SocioId = socioId,
                GimnasioId = gimnasio.Id,
                NombreSocio = socio.NombreCompleto,
                DniDeclarado = socio.Dni
            };
            await _solicitudes.AddAsync(solicitud);
            await _audit.RegistrarAsync("SOLICITUD_VINCULACION", nameof(SolicitudVinculacion), solicitud.Id.ToString(), gimnasio.Nombre);
            return Mapear(solicitud);
        }

        public async Task<IEnumerable<SolicitudVinculacionDto>> ListarMisSolicitudesAsync()
        {
            var socioId = _actual.SocioId ?? throw new AccesoDenegadoException();
            return (await _solicitudes.GetDeSocioAsync(socioId)).Select(Mapear);
        }

        public async Task<IEnumerable<SolicitudVinculacionDto>> ListarPendientesAsync(Guid? gimnasioId = null)
        {
            var destino = await GimnasioDestino.ResolverAsync(_actual, _gimnasios, gimnasioId);
            var lista = EsPlataforma
                ? await _solicitudes.GetPendientesDeGimnasioExplicitoAsync(destino)
                : await _solicitudes.GetPendientesDeGimnasioAsync(destino);
            return lista.Select(Mapear);
        }

        public async Task<SolicitudVinculacionDto> ResolverAsync(Guid solicitudId, bool aprobar, string? motivo, Guid? gimnasioIndicado = null)
        {
            var gimnasioId = await GimnasioDestino.ResolverAsync(_actual, _gimnasios, gimnasioIndicado);
            var usuarioId = _actual.UsuarioId ?? throw new AccesoDenegadoException();

            var solicitud = EsPlataforma
                ? await _solicitudes.GetEnGimnasioExplicitoAsync(solicitudId, gimnasioId)
                : await _solicitudes.GetByIdAsync(solicitudId);
            if (solicitud is null || solicitud.GimnasioId != gimnasioId)
                throw new RecursoNoEncontradoException("Solicitud no encontrada.");

            if (aprobar)
            {
                var socio = await _socios.GetForUpdateAsync(solicitud.SocioId)
                    ?? throw new RecursoNoEncontradoException("Socio no encontrado.");

                // Validación cruzada: el DNI declarado en la solicitud debe coincidir con el de la ficha del socio.
                if (!string.Equals(socio.Dni, solicitud.DniDeclarado, StringComparison.Ordinal))
                    throw new ReglaNegocioException("El DNI de la solicitud no coincide con el de la ficha del socio.");
                if (socio.GimnasioId is not null)
                    throw new ReglaNegocioException("El socio ya está vinculado a un gimnasio.");

                socio.GimnasioId = gimnasioId;
                socio.FechaModificacion = DateTime.UtcNow;
            }

            solicitud.Resolver(aprobar, usuarioId, motivo);
            await _solicitudes.SaveChangesAsync(); // misma unidad de trabajo: socio  solicitud
            await _audit.RegistrarEnGimnasioAsync(aprobar ? "VINCULACION_APROBADA" : "VINCULACION_RECHAZADA",
                nameof(SolicitudVinculacion), solicitud.Id.ToString(), gimnasioId, motivo);
            return Mapear(solicitud);
        }

        private bool EsPlataforma => _actual.Rol == RolUsuario.SuperAdministrador;

        private static SolicitudVinculacionDto Mapear(SolicitudVinculacion s) => new()
        {
            Id = s.Id,
            SocioId = s.SocioId,
            NombreSocio = s.NombreSocio,
            Dni = s.DniDeclarado,
            GimnasioId = s.GimnasioId,
            Estado = s.Estado.ToString(),
            FechaAlta = s.FechaAlta,
            Motivo = s.Motivo
        };
    }
}