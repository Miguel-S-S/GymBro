using GymBro.Domain.Enums;

namespace GymBro.Domain.Entities
{
    public class MatrizRecomendacion
    {
        public int Id { get; set; }

        public ObjetivoEntrenamiento Objetivo { get; set; }

        public bool AplicaHipertension { get; set; }
        public bool AplicaDiabetes { get; set; }
        public string TipoEntrenamiento { get; set; } = string.Empty;
        public string SugerenciaDieta { get; set; } = string.Empty;
        public string RecomendacionGeneral { get; set; } = string.Empty;
    }
}