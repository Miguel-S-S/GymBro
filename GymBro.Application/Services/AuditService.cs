using GymBro.Application.DTOs.Acceso;
using GymBro.Domain.Entities;
using GymBro.Domain.Repositories;
using GymBro.Domain.Security;
namespace GymBro.Application.Services
{
    public interface IAuditService
    {
        Task RegistrarAsync(string accion, string entidad, string? entidadId = null, string? detalle = null);

        /// <summary>
        /// Etapa 9a-2b: registra la acción sobre un gimnasio indicado explícitamente (el Administrador de la Plataforma no
        /// tiene gimnasio en el JWT). Si quien actúa es el Administrador de la Plataforma, el detalle lo indica.
        /// </summary>
        Task RegistrarEnGimnasioAsync(string accion, string entidad, string? entidadId, Guid? gimnasioId, string? detalle = null);
        Task<IEnumerable<AuditLogDto>> ConsultarAsync(int cantidad, Guid? gimnasioId, DateTime? desdeUtc = null, DateTime? hastaUtc = null, string? accion = null, string? entidad = null);
    }

    public class AuditService : IAuditService
    {
        private readonly IAuditLogRepository _repo;
        private readonly IUsuarioActual _actual;

        public AuditService(IAuditLogRepository repo, IUsuarioActual actual)
        {
            _repo = repo;
            _actual = actual;
        }

        public Task RegistrarAsync(string accion, string entidad, string? entidadId = null, string? detalle = null) =>
            _repo.AddAsync(new AuditLog
            {
                UsuarioId = _actual.UsuarioId,
                Rol = _actual.Rol?.ToString(),
                GimnasioId = _actual.GimnasioId,
                Accion = accion,
                Entidad = entidad,
                EntidadId = entidadId,
                Detalle = detalle
            });

        public Task RegistrarEnGimnasioAsync(string accion, string entidad, string? entidadId, Guid? gimnasioId, string? detalle = null)
        {
            var plataforma = _actual.Rol == Domain.Enums.RolUsuario.SuperAdministrador;
            var texto = plataforma
                ? $"Administrador de la Plataforma sobre {(gimnasioId is null ? "el catálogo global" : $"el gimnasio {gimnasioId}")}"
                  + (string.IsNullOrEmpty(detalle) ? "" : $". {detalle}")
                : detalle;
            return _repo.AddAsync(new AuditLog
            {
                UsuarioId = _actual.UsuarioId,
                Rol = _actual.Rol?.ToString(),
                GimnasioId = gimnasioId ?? _actual.GimnasioId,
                Accion = accion,
                Entidad = entidad,
                EntidadId = entidadId,
                Detalle = texto
            });
        }

        public async Task<IEnumerable<AuditLogDto>> ConsultarAsync(int cantidad, Guid? gimnasioId,
            DateTime? desdeUtc = null, DateTime? hastaUtc = null, string? accion = null, string? entidad = null)
        {
            cantidad = Math.Clamp(cantidad, 1, 500);
            var items = await _repo.GetRecientesAsync(cantidad, gimnasioId, desdeUtc, hastaUtc, accion, entidad);
            return items.Select(a => new AuditLogDto
            {
                Id = a.Id,
                Fecha = a.Fecha,
                UsuarioId = a.UsuarioId,
                Rol = a.Rol,
                GimnasioId = a.GimnasioId,
                Accion = a.Accion,
                Entidad = a.Entidad,
                EntidadId = a.EntidadId,
                Detalle = a.Detalle
            });
        }
    }
}