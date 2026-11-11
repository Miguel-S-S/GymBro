using GymBro.Domain.Enums;

namespace GymBro.Domain.Entities
{
    /// <summary>Traza de cada decisión humana sobre una rutina generada por la IA.</summary>
    public class RevisionPrescripcion
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid PrescripcionEntrenamientoId { get; set; }
        public PrescripcionEntrenamiento PrescripcionEntrenamiento { get; set; } = null!;
        public Guid RevisorUsuarioId { get; set; }
        public EstadoPrescripcion Decision { get; set; }
        public string? Comentario { get; set; }
        public DateTime Fecha { get; set; } = DateTime.UtcNow;
    }
}