using System;
using System.Collections.Generic;
using System.Text;

namespace GymBro.Domain.Entities
{
    //mapea series.csv
    public class DosificacionVolumetrica : EntidadAuditable
    {
        public Guid Id { get; set; }
        public string ParteCuerpo { get; set; } = string.Empty;
        public string SubzonaObjetivo { get; set; } = string.Empty;
        public string ZonaObjetivo { get; set; } = string.Empty; // Target_Zone

        public int SeriesMinimas { get; set; }
        public int SeriesMaximas { get; set; }
        public int RepeticionesMinimas { get; set; }
        public int RepeticionesMaximas { get; set; }
    }
}
