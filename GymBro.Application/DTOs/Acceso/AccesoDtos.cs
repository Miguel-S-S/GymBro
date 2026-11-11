namespace GymBro.Application.DTOs.Acceso
{
    public record LoginRequest(string Email, string Password);

    public class LoginResponse
    {
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiraUtc { get; set; }
        public string Rol { get; set; } = string.Empty;
        public string NombreCompleto { get; set; } = string.Empty;
        public Guid? GimnasioId { get; set; }
        public Guid? SocioId { get; set; }
    }

    public class RegistroSocioRequest
    {
        public string Nombre { get; set; } = string.Empty;
        public string Apellido { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Dni { get; set; } = string.Empty;
        public DateTime FechaNacimiento { get; set; }
        /// <summary>M, F u X.</summary>
        public string Genero { get; set; } = string.Empty;
        /// <summary>Consentimiento para tratar datos de salud (Ley 25.326). Obligatorio.</summary>
        public bool ConsentimientoDatosSalud { get; set; }
    }

    public class GimnasioCreateDto
    {
        public string Nombre { get; set; } = string.Empty;
        public string DuenoNombreCompleto { get; set; } = string.Empty;
        public string DuenoEmail { get; set; } = string.Empty;
        public string DuenoPassword { get; set; } = string.Empty;
        public bool RequiereAprobacionRutinas { get; set; } = true;
    }

    public class GimnasioResponseDto
    {
        public Guid Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Codigo { get; set; } = string.Empty;
        public bool RequiereAprobacionRutinas { get; set; }
        public bool Activo { get; set; }
        public DateTime FechaAlta { get; set; }
    }

    public record PoliticaRevisionRequest(bool RequiereAprobacionRutinas);

    public class EntrenadorCreateDto
    {
        public string NombreCompleto { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class EntrenadorResponseDto
    {
        public Guid Id { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool Activo { get; set; }
    }

    public record AsignacionRequest(Guid SocioId);

    public record SolicitarVinculacionRequest(string CodigoGimnasio);

    public class SolicitudVinculacionDto
    {
        public Guid Id { get; set; }
        public Guid SocioId { get; set; }
        public string NombreSocio { get; set; } = string.Empty;
        public string Dni { get; set; } = string.Empty;
        public Guid GimnasioId { get; set; }
        public string Estado { get; set; } = string.Empty;
        public DateTime FechaAlta { get; set; }
        public string? Motivo { get; set; }
    }

    public record ResolverSolicitudRequest(bool Aprobar, string? Motivo);

    public class RutinaPendienteDto
    {
        public Guid Id { get; set; }
        public Guid SocioId { get; set; }
        public string NombreSocio { get; set; } = string.Empty;
        public string OrigenGeneracion { get; set; } = string.Empty;
        public string? Observaciones { get; set; }
        public string Estado { get; set; } = string.Empty;
        public DateTime FechaAlta { get; set; }
        public List<RutinaEjercicioDto> Ejercicios { get; set; } = new();
    }

    public class RutinaEjercicioDto
    {
        public Guid Id { get; set; }
        public int Orden { get; set; }
        public string Ejercicio { get; set; } = string.Empty;
        public int SeriesObjetivo { get; set; }
        public int RepeticionesMinimas { get; set; }
        public int RepeticionesMaximas { get; set; }
        public int? RIRObjetivo { get; set; }
        public int DescansoObjetivoSegundos { get; set; }
        public string? Observaciones { get; set; }
    }

    public class AjusteEjercicioDto
    {
        public Guid PrescripcionEjercicioId { get; set; }
        public int? SeriesObjetivo { get; set; }
        public int? RepeticionesMinimas { get; set; }
        public int? RepeticionesMaximas { get; set; }
        public int? RIRObjetivo { get; set; }
        public int? DescansoObjetivoSegundos { get; set; }
        public string? Observaciones { get; set; }
    }

    public class RevisarRutinaRequest
    {
        /// <summary>Aprobada, Editada o Rechazada.</summary>
        public string Decision { get; set; } = string.Empty;
        public string? Comentario { get; set; }
        public List<AjusteEjercicioDto> Ajustes { get; set; } = new();
    }

    public class AuditLogDto
    {
        public Guid Id { get; set; }
        public DateTime Fecha { get; set; }
        public Guid? UsuarioId { get; set; }
        public string? Rol { get; set; }
        public Guid? GimnasioId { get; set; }
        public string Accion { get; set; } = string.Empty;
        public string Entidad { get; set; } = string.Empty;
        public string? EntidadId { get; set; }
        public string? Detalle { get; set; }
    }
}