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
        public int Repeticiones { get; set; }
        public int? RIR { get; set; }
        public bool EsCalentamiento { get; set; }
        public int? TiempoDescansoSegundos { get; set; }
    }
}
