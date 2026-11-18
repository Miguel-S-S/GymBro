using System;

namespace GymBro.Domain.Entities
{
    public class SerieRealizada : EntidadAuditable
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid SesionEjercicioId { get; set; }
        public SesionEjercicio SesionEjercicio { get; set; } = null!;

        public int OrdenSerie { get; set; }
        public decimal PesoLevantadoKg { get; set; }
        /// <summary>
        /// Repeticiones realizadas. Si el ejercicio se mide en segundos (<c>Ejercicio.TipoMedida</c>), guarda los segundos
        /// sostenidos (decisión de diseño de la Etapa 9a-1); en ese caso no se calcula volumen en kg.
        /// </summary>
        public int Repeticiones { get; set; }
        public int? RIR { get; set; }
        public bool EsCalentamiento { get; set; }
        public int? TiempoDescansoSegundos { get; set; }
    }
}
