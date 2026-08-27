using GymBro.Domain.Enums;

namespace GymBro.Domain.Entities
{
    public class MedicionFisica : EntidadAuditable
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid SocioId { get; set; }
        public Socio Socio { get; set; } = null!;

        //metricas de composicion corporal
        public decimal PesoKg { get; set; }
        public decimal AlturaCm { get; set; }
        public decimal PorcentajeGrasa { get; set; }
        public decimal IMC { get; set; }

        //metricas de entrenamiento y esfuerzo
        public int FrecuenciaEntrenamientoDias { get; set; }
        public TipoEntrenamiento TipoEntrenamiento { get; set; }
        public decimal IngestaAguaLitros { get; set; }

        //metricas cardiovasculares
        public int FrecuenciaCardiacaReposo { get; set; }
        public decimal PresionArterialSistolica { get; set; }
        public decimal PresionArterialDiastolica { get; set; }
        public decimal PresionArterialMedia { get; set; } // MAP_mmHg

        public string CondicionMedica { get; set; } = string.Empty; //health condition
        public int NivelEstres { get; set; } // 1-10
        public decimal HorasSuenoUltimaNoche { get; set; }
        
        //banderas deriva del motor de reglas
        public bool TieneHipertension => CondicionMedica.Contains("Hipertension", StringComparison.OrdinalIgnoreCase);
        public bool TieneDiabetes => CondicionMedica.Contains("Diabetes", StringComparison.OrdinalIgnoreCase);
    }
}