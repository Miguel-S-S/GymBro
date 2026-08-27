using GymBro.Domain.Enums;

namespace GymBro.Domain.Entities
{
    // Mapea gym_recommendation.xlsx
    public class MatrizRecomendacion : EntidadAuditable
    {
        public int Id { get; set; }
        public string Genero { get; set; } = string.Empty;
        public int Edad { get; set; }
        public bool AplicaHipertension { get; set; }
        public bool AplicaDiabetes { get; set; }

        public CategoriaIMC CategoriaIMC { get; set; }
        public ObjetivoEntrenamiento Objetivo { get; set; }

        public string CategoriaRutina { get; set; } = string.Empty; // Routine_Category
        public string SugerenciaDieta { get; set; } = string.Empty;
        public string RecomendacionGeneral { get; set; } = string.Empty;
    }
}