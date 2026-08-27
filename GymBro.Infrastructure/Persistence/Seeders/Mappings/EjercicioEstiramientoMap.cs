using CsvHelper.Configuration;
using GymBro.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace GymBro.Infrastructure.Persistence.Seeders.Mappings
{
    public class EjercicioEstiramientoMap : ClassMap<Ejercicio>
    {
        public EjercicioEstiramientoMap()
        {
            Map(m => m.Id).Convert(args => Guid.NewGuid());
            Map(m => m.Nombre).Name("Exercise Name");
            Map(m => m.Equipamiento).Name("Equipment");

            // Asignamos ZonaObjetivo como "Movilidad" para diferenciarlos
            Map(m => m.ZonaObjetivo).Constant("Movilidad");
            Map(m => m.MusculoPrincipal).Name("Main_muscle");
            Map(m => m.Sinergistas).Name("Synergist_Muscles");
            Map(m => m.Estabilizadores).Constant("Ninguno");
            Map(m => m.Preparacion).Name("Preparation");
            Map(m => m.Ejecucion).Name("Execution");
            Map(m => m.Dificultad).Constant(1); // Los estiramientos suelen ser de dificultad base

            Map(m => m.Embedding).Ignore();
        }
    }
}
