namespace GymBro.Domain.Entities
{
    public class MedicionFisica : EntidadAuditable
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid SocioId { get; set; }

        public decimal PesoKg { get; set; }
        public decimal AlturaCm { get; set; }
        public decimal PorcentajeGrasa { get; set; }

        public int FrecuenciaCardiacaReposo { get; set; }

        public int PresionArterialSistolica { get; set; }

        public int PresionArterialDiastolica { get; set; }

        public decimal HorasSuenoUltimaNoche { get; set; }

        public bool TieneHipertension { get; set; }

        public bool TieneDiabetes { get; set; }

        public decimal IMC =>
            AlturaCm > 0
                ? PesoKg / ((AlturaCm / 100) * (AlturaCm / 100))
                : 0;

        public Socio Socio { get; set; } = null!;
    }
}