namespace GymBro.Domain.Entities
{
    public class SesionEjercicio : EntidadAuditable
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid SesionId { get; set; }
        public SesionEntrenamiento Sesion { get; set; } = null!;

        public Guid EjercicioId { get; set; }
        public Ejercicio Ejercicio { get; set; } = null!;

        public int Orden { get; set; }

        /// <summary>
        /// Etapa 7d (6.3): línea de la prescripción que se ejecutó. Null en las sesiones anteriores a la etapa y en las
        /// que no tienen prescripción; en ese caso se sigue relacionando por ejercicio y orden.
        /// </summary>
        public Guid? PrescripcionEjercicioId { get; set; }
        public PrescripcionEjercicio? PrescripcionEjercicio { get; set; }
        public string? Observaciones { get; set; }

        public ICollection<SerieRealizada> SeriesRealizadas { get; set; } = new List<SerieRealizada>();
    }
}