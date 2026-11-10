using System;

namespace GymBro.Domain.Entities
{
    public class PrescripcionEjercicio : EntidadAuditable
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid PrescripcionEntrenamientoId { get; set; }
        public PrescripcionEntrenamiento PrescripcionEntrenamiento { get; set; } = null!;

        public Guid EjercicioId { get; set; }
        public Ejercicio Ejercicio { get; set; } = null!;

        public int Orden { get; set; }
        public int SeriesObjetivo { get; set; }
        public int RepeticionesMinimas { get; set; }
        public int RepeticionesMaximas { get; set; }
        public int? RIRObjetivo { get; set; }
        public int DescansoObjetivoSegundos { get; set; }

        public string? Observaciones { get; set; }
    }
}
