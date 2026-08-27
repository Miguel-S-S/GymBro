using System;
using System.Collections.Generic;
using System.Text;

namespace GymBro.Domain.Enums
{
    public enum ObjetivoEntrenamiento
    {
        PerdidaPeso = 1, //weight loss
        GananciaMuscular = 2, //wieght gain
        Mantenimiento = 3 // maintenance
    }

    public enum NivelExperiencia
    {
        Principiante = 1,
        Intermedio = 2,
        Avanzado = 3
    }

    public enum CategoriaIMC
    {
        BajoPeso = 1, //underweight
        Normal = 2, // normal
        Sobrepeso = 3, // overweight
        Obeso = 4 // obese
    }

    public enum TipoEntrenamiento
    {
        Fuerza = 1,
        Cardio = 2,
        HIIT = 3
    }
}
