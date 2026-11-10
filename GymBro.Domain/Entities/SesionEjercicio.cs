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
        public string? Observaciones { get; set; }

        public ICollection<SerieRealizada> SeriesRealizadas { get; set; } = new List<SerieRealizada>();
    }
}