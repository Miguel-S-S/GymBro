using System;

namespace GymBro.Application.DTOs
{
    public class MedicionResponseDto
    {
        public Guid Id { get; set; }
        public Guid SocioId { get; set; }
        public DateTime FechaAlta { get; set; }

        public decimal PesoKg { get; set; }
        public decimal AlturaCm { get; set; }
        public decimal PorcentajeGrasa { get; set; }
        public decimal IMC { get; set; }

        public int FrecuenciaEntrenamientoDias { get; set; }
        public string? TipoEntrenamiento { get; set; }
        public decimal IngestaAguaLitros { get; set; }

        public int FrecuenciaCardiacaReposo { get; set; }
        public decimal PresionArterialSistolica { get; set; }
        public decimal PresionArterialDiastolica { get; set; }
        public decimal PresionArterialMedia { get; set; }

        public string CondicionMedica { get; set; } = string.Empty;
        public int NivelEstres { get; set; }
        public decimal HorasSuenoUltimaNoche { get; set; }
    }
}
