using GymBro.Domain.Enums;

namespace GymBro.Domain.Entities
{
    /// <summary>Cuenta de acceso. Un Socio-usuario referencia su ficha en SocioId.</summary>
    public class Usuario : EntidadAuditable
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string NombreCompleto { get; set; } = string.Empty;
        public RolUsuario Rol { get; set; }

        /// <summary>null para SuperAdministrador y socios independientes.</summary>
        public Guid? GimnasioId { get; set; }
        public Gimnasio? Gimnasio { get; set; }

        /// <summary>Solo para Rol = Socio.</summary>
        public Guid? SocioId { get; set; }
        public Socio? Socio { get; set; }

        public bool Activo { get; set; } = true;
    }
}