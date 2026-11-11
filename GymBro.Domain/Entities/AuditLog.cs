namespace GymBro.Domain.Entities
{
    /// <summary>Registro append-only de acciones relevantes. Lo consulta el SuperAdministrador.</summary>
    public class AuditLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime Fecha { get; set; } = DateTime.UtcNow;
        public Guid? UsuarioId { get; set; }
        public string? Rol { get; set; }
        public Guid? GimnasioId { get; set; }
        public string Accion { get; set; } = string.Empty;
        public string Entidad { get; set; } = string.Empty;
        public string? EntidadId { get; set; }
        public string? Detalle { get; set; }
    }
}