namespace GymBro.Domain.Entities
{
    public class SesionEntrenamiento : EntidadAuditable
    {
        public Guid Id { get; set; }
        public Guid SocioId { get; set; }
        public DateTime Fecha { get; set; }

        public int DuracionMinutos { get; set; }
        public int CaloriasQuemadas { get; set; }
        public int FrecuenciaCardiacaMaxima { get; set; }
        public int FrecuenciaCardiacaPromedio { get; set; }

        // Relación muchos a muchos con los ejercicios ejecutados en esta sesión
        public ICollection<SesionEjercicio> EjerciciosEjecutados { get; set; } = new List<SesionEjercicio>();
    }
}