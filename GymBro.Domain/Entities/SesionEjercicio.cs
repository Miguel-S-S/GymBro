namespace GymBro.Domain.Entities
{
    public class SesionEjercicio
    {
        public Guid SesionId { get; set; }
        public SesionEntrenamiento Sesion { get; set; } = null!;

        public Guid EjercicioId { get; set; }
        public Ejercicio Ejercicio { get; set; } = null!;

        public int Series { get; set; }
        public int Repeticiones { get; set; }
        public decimal PesoLevantadoKg { get; set; }
        public int TiempoDescansoSegundos { get; set; }
    }
}