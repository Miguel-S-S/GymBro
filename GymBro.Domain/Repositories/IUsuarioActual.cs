using GymBro.Domain.Enums;

namespace GymBro.Domain.Security
{
    /// <summary>
    /// Identidad del request en curso. Es la base del aislamiento multi-tenant:
    /// el DbContext la usa en los filtros globales de consulta.
    /// </summary>
    public interface IUsuarioActual
    {
        bool EsAutenticado { get; }

        /// <summary>true cuando no hay request HTTP (migraciones, seeders). Ve todos los tenants.</summary>
        bool EsProcesoDeSistema { get; }

        Guid? UsuarioId { get; }
        RolUsuario? Rol { get; }
        Guid? GimnasioId { get; }
        Guid? SocioId { get; }
    }
}