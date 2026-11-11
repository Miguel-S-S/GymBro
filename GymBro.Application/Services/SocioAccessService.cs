using GymBro.Domain.Enums;
using GymBro.Domain.Exceptions;
using GymBro.Domain.Repositories;
using GymBro.Domain.Security;

namespace GymBro.Application.Services
{
    /// <summary>
    /// Autorización a nivel de recurso sobre los datos de un socio.
    /// El filtro multi-tenant del DbContext aísla gimnasios; este servicio agrega el alcance por rol.
    /// </summary>
    public interface ISocioAccessService
    {
        Task AsegurarAccesoAsync(Guid socioId);
    }

    public class SocioAccessService : ISocioAccessService
    {
        private readonly IUsuarioActual _actual;
        private readonly ISocioRepository _socios;
        private readonly IAsignacionRepository _asignaciones;

        public SocioAccessService(IUsuarioActual actual, ISocioRepository socios, IAsignacionRepository asignaciones)
        {
            _actual = actual;
            _socios = socios;
            _asignaciones = asignaciones;
        }

        public async Task AsegurarAccesoAsync(Guid socioId)
        {
            if (_actual.EsProcesoDeSistema) return;
            if (!_actual.EsAutenticado) throw new AccesoDenegadoException();

            var permitido = _actual.Rol switch
            {
                // El socio solo ve lo suyo.
                RolUsuario.Socio => _actual.SocioId == socioId,
                // El trainer solo ve sus socios asignados.
                RolUsuario.Entrenador => _actual.UsuarioId is Guid uid && await _asignaciones.EstaAsignadoAsync(uid, socioId),
                // El dueño ve los socios de su gimnasio (el filtro de tenant ya limita la consulta).
                RolUsuario.AdministradorGimnasio => await _socios.ExistsAsync(socioId),
                // El administrador de la plataforma NO accede a datos de salud de los socios (mínimo privilegio).
                _ => false
            };

            if (!permitido) throw new AccesoDenegadoException();
        }
    }
}