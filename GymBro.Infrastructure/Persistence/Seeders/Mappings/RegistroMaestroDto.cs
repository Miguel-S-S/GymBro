using CsvHelper.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GymBro.Domain.Entities;

namespace GymBro.Infrastructure.Persistence.Seeders.Mappings
{
    public class RegistroMaestroDto
    {
        public string Gender { get; set; } = string.Empty;
        public string Birth_Date { get; set; } = string.Empty;
        public string Workout_Type { get; set; } = string.Empty;
        public string health_condition { get; set; } = string.Empty;

        public decimal Weight_kg { get; set; }
        public decimal Height_cm { get; set; }
        public decimal Fat_Percentage { get; set; }
        public decimal MAP_mmHg { get; set; }
        public int Resting_BPM { get; set; }
    }

    public class RegistroMaestroMap : ClassMap<RegistroMaestroDto>
    {
        public RegistroMaestroMap() 
        {
            AutoMap(CultureInfo.InvariantCulture);
        }
    }
}
