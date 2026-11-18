using System;
using System.Collections.Generic;
using System.Text;
using GymBro.Domain.Enums;

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
        /// <summary>Repeticiones mínimas; si <see cref="TipoMedida"/> es Segundos, segundos mínimos (Etapa 9a-1).</summary>
        public int RepeticionesMinimas { get; set; }
        /// <summary>Repeticiones máximas; si <see cref="TipoMedida"/> es Segundos, segundos máximos (Etapa 9a-1).</summary>
        public int RepeticionesMaximas { get; set; }

        /// <summary>Unidad de la fila: la dosis se elige según el tipo de medida del ejercicio (Etapa 9a-1).</summary>
        public TipoMedida TipoMedida { get; set; } = TipoMedida.Repeticiones;
    }
}
