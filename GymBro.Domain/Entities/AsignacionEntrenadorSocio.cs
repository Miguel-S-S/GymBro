namespace GymBro.Domain.Entities
{
    /// <summary>Un Personal Trainer solo accede a los socios que tiene asignados.</summary>
    public class AsignacionEntrenadorSocio : EntidadAuditable
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid GimnasioId { get; set; }
        public Guid EntrenadorUsuarioId { get; set; }
        public Usuario EntrenadorUsuario { get; set; } = null!;
        public Guid SocioId { get; set; }
        public Socio Socio { get; set; } = null!;
    }
}