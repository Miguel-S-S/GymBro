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
        /// <summary>
        /// Repeticiones mínimas. Si el ejercicio se mide en segundos (<c>Ejercicio.TipoMedida</c>), guarda segundos:
        /// decisión de diseño de la Etapa 9a-1 para no renombrar columnas; el cliente muestra la unidad según el tipo.
        /// </summary>
        public int RepeticionesMinimas { get; set; }
        /// <summary>Repeticiones máximas; segundos si el ejercicio se mide en segundos (ver <see cref="RepeticionesMinimas"/>).</summary>
        public int RepeticionesMaximas { get; set; }
        public int? RIRObjetivo { get; set; }
        public int DescansoObjetivoSegundos { get; set; }

        public string? Observaciones { get; set; }
    }
}
