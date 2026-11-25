using GymBro.Domain.Enums;

namespace GymBro.Domain.Entities
{
    /// <summary>
    /// Etapa 11: cuota mensual de un socio en un gimnasio. Una cuota por socio y periodo (anio-mes) entre las filas
    /// vigentes. No hay pasarela de pago: el Dueno registra el pago a mano. La morosidad se deriva (EstaVencida).
    /// </summary>
    public class CuotaSocio : EntidadAuditable
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid SocioId { get; set; }
        public Socio Socio { get; set; } = null!;

        public Guid GimnasioId { get; set; }

        /// <summary>Periodo: anio (2000 a 2100) y mes (1 a 12).</summary>
        public int Anio { get; set; }
        public int Mes { get; set; }

        public decimal Monto { get; set; }
        public DateOnly Vencimiento { get; set; }

        /// <summary>Dia local del pago; null mientras esta Pendiente.</summary>
        public DateOnly? FechaPago { get; set; }

        public EstadoCuota Estado { get; set; } = EstadoCuota.Pendiente;

        /// <summary>Procedencia del dato (Etapa 7e: el generador sintetico usa Sintetico).</summary>
        public OrigenDatos OrigenDatos { get; set; } = OrigenDatos.Real;

        /// <summary>Vencida (morosa): pendiente con vencimiento anterior a hoy. El dia de vencimiento todavia no esta vencida.</summary>
        public bool EstaVencida(DateOnly hoy) => Estado == EstadoCuota.Pendiente && Vencimiento < hoy;
    }

    /// <summary>Limites de la cuota. Se aplican en el servicio y como CHECK en la base.</summary>
    public static class RangosCuota
    {
        public const int AnioMin = 2000, AnioMax = 2100;
        public const decimal MontoMin = 0.01m, MontoMax = 99_999_999.99m;
    }
}
