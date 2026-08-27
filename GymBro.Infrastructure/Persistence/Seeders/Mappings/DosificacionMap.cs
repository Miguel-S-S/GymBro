using System;
using System.Collections.Generic;
using System.Text;
using CsvHelper.Configuration;
using GymBro.Domain.Entities;

namespace GymBro.Infrastructure.Persistence.Seeders.Mappings
{
    public class DosificacionMap : ClassMap<DosificacionVolumetrica>
    {
        public DosificacionMap()
        {
            Map(m => m.Id).Convert(args => Guid.NewGuid());
            Map(m => m.ParteCuerpo).Name("Body_Part");
            Map(m => m.SubzonaObjetivo).Name("Target_Subzone");
            Map(m => m.ZonaObjetivo).Name("Target_Zone");
            Map(m => m.SeriesMinimas).Name("Min_Sets");
            Map(m => m.SeriesMaximas).Name("Max_Sets");
            Map(m => m.RepeticionesMinimas).Name("Min_Reps");
            Map(m => m.RepeticionesMaximas).Name("Max_Reps");
        }
    }
}
