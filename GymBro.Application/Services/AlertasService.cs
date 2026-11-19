using System.Text.Json;
using GymBro.Application.DTOs;
using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Domain.Exceptions;
using GymBro.Domain.Repositories;
using GymBro.Domain.Security;

namespace GymBro.Application.Services
{
    public interface IAlertasService
    {
        /// <summary>
        /// Socio: las suyas (también si está vinculado a un gimnasio). Personal Trainer: las de sus socios asignados.
        /// Dueño: las de su gimnasio. Con socioId, solo las de ese socio (404 si no es visible para quien llama).
        /// </summary>
        Task<IReadOnlyList<AlertaSeguridadDto>> ListarAsync(Guid? socioId, string? estado);

        /// <summary>Marca la alerta como vista. 404 si no es visible. Queda en la auditoría sin valores de salud.</summary>
        Task<AlertaSeguridadDto> MarcarVistaAsync(Guid id);
    }

    /// <summary>
    /// Consulta y seguimiento de las alertas de seguridad (Etapa 10a). Son datos de salud: el Administrador de la
    /// Plataforma no accede (403 en el controlador y aquí, como defensa), y el filtro global limita al socio o al gimnasio.
    /// </summary>
    public class AlertasService : IAlertasService
    {
        private readonly IAlertaSeguridadRepository _repo;
        private readonly IAsignacionRepository _asignaciones;
        private readonly ISocioAccessService _acceso;
        private readonly IUsuarioActual _actual;
        private readonly IAuditService _audit;
        private readonly TimeProvider _reloj;

        public AlertasService(IAlertaSeguridadRepository repo, IAsignacionRepository asignaciones, ISocioAccessService acceso,
            IUsuarioActual actual, IAuditService audit, TimeProvider reloj)
        {
            _repo = repo;
            _asignaciones = asignaciones;
            _acceso = acceso;
            _actual = actual;
            _audit = audit;
            _reloj = reloj;
        }

        public async Task<IReadOnlyList<AlertaSeguridadDto>> ListarAsync(Guid? socioId, string? estado)
        {
            var filtroEstado = LeerEstado(estado);
            IReadOnlyCollection<Guid>? socios;

            switch (_actual.Rol)
            {
                case RolUsuario.Socio:
                    var propio = _actual.SocioId ?? throw new AccesoDenegadoException();
                    if (socioId is not null && socioId != propio) throw new RecursoNoEncontradoException("Socio no encontrado.");
                    socios = new[] { propio };
                    break;
                case RolUsuario.AdministradorGimnasio or RolUsuario.Entrenador when socioId is Guid id:
                    await _acceso.AsegurarAccesoAsync(id);          // 404 si no es visible (otro gimnasio o no asignado)
                    socios = new[] { id };
                    break;
                case RolUsuario.Entrenador:
                    var uid = _actual.UsuarioId ?? throw new AccesoDenegadoException();
                    socios = (await _asignaciones.GetSocioIdsAsync(uid)).ToList();
                    break;
                case RolUsuario.AdministradorGimnasio:
                    socios = null;                                   // el filtro global limita al gimnasio
                    break;
                default:
                    throw new AccesoDenegadoException("Las alertas de seguridad son datos de salud.");
            }

            var lista = await _repo.ListarAsync(socios, filtroEstado);
            return lista.Select(Mapear).ToList();
        }

        public async Task<AlertaSeguridadDto> MarcarVistaAsync(Guid id)
        {
            if (_actual.Rol is not (RolUsuario.Socio or RolUsuario.AdministradorGimnasio or RolUsuario.Entrenador))
                throw new AccesoDenegadoException("Las alertas de seguridad son datos de salud.");

            var alerta = await _repo.GetVisibleAsync(id) ?? throw new RecursoNoEncontradoException("Alerta no encontrada.");
            await _acceso.AsegurarAccesoAsync(alerta.SocioId);    // el Personal Trainer, solo de sus asignados

            if (alerta.Estado != EstadoAlerta.Vista)
            {
                var ahora = _reloj.GetUtcNow().UtcDateTime;
                alerta.Estado = EstadoAlerta.Vista;
                alerta.VistaPorUsuarioId = _actual.UsuarioId;
                alerta.VistaEn = ahora;
                alerta.FechaModificacion = ahora;
                await _repo.GuardarCambiosAsync();
                // Sin valores de salud: solo el código de la regla.
                await _audit.RegistrarAsync("ALERTA_VISTA", nameof(AlertaSeguridad), alerta.Id.ToString(), alerta.ReglaCodigo);
            }
            return Mapear(alerta);
        }

        /// <summary>Solo los nombres Pendiente o Vista (sin números ni listas), como LeerDecision de la revisión.</summary>
        private static EstadoAlerta? LeerEstado(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return null;
            foreach (var e in Enum.GetValues<EstadoAlerta>())
                if (string.Equals(e.ToString(), texto.Trim(), StringComparison.OrdinalIgnoreCase)) return e;
            throw new ReglaNegocioException("El estado debe ser Pendiente o Vista.");
        }

        public static AlertaSeguridadDto Mapear(AlertaSeguridad a)
        {
            var detalle = JsonDocument.Parse(string.IsNullOrWhiteSpace(a.Detalle) ? "{}" : a.Detalle).RootElement.Clone();
            string? ejercicio = detalle.TryGetProperty("ejercicio", out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() : null;
            decimal? carga = detalle.TryGetProperty("resultado", out var r) && r.ValueKind == JsonValueKind.Object
                             && r.TryGetProperty("carga_sugerida_kg", out var c) && c.ValueKind == JsonValueKind.Number ? c.GetDecimal() : null;
            return new AlertaSeguridadDto
            {
                Id = a.Id,
                SocioId = a.SocioId,
                NombreSocio = a.Socio?.NombreCompleto ?? string.Empty,
                ReglaCodigo = a.ReglaCodigo,
                Tipo = a.Tipo.ToString(),
                Severidad = a.Severidad.ToString(),
                Mensaje = a.Mensaje,
                Estado = a.Estado.ToString(),
                FechaAlta = a.FechaAlta,
                VistaEn = a.VistaEn,
                SesionId = a.SesionId,
                SesionEjercicioId = a.SesionEjercicioId,
                RegistroBienestarId = a.RegistroBienestarId,
                Ejercicio = ejercicio,
                CargaSugeridaKg = carga,
                Detalle = detalle,
            };
        }
    }
}
