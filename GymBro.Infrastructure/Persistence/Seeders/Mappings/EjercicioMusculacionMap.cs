using CsvHelper.Configuration;
using GymBro.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace GymBro.Infrastructure.Persistence.Seeders.Mappings
{
    public class EjercicioMusculacionMap : ClassMap<Ejercicio>
    {
        public EjercicioMusculacionMap()
        {
            Map(m => m.Id).Convert(args => Guid.NewGuid());
            Map(m => m.Nombre).Name("Exercise Name");
            Map(m => m.Equipamiento).Name("Equipment");
            Map(m => m.ZonaObjetivo).Name("Target_Zone");
            Map(m => m.MusculoPrincipal).Name("Main_muscle");
            Map(m => m.Sinergistas).Name("Synergist_Muscles");
            Map(m => m.Estabilizadores).Name("Stabilizer_Muscles");
            Map(m => m.Preparacion).Name("Preparation");
            Map(m => m.Ejecucion).Name("Execution");
            Map(m => m.Dificultad).Name("Difficulty (1-5)").Default(1);

            // Ignoramos el campo Embedding durante la carga inicial
            Map(m => m.Embedding).Ignore();
        }
    }
}
