using GymBro.Domain.Rules.Kpis;

namespace GymBro.Application.DTOs
{
    /// <summary>Limitaciones articulares del socio (Etapa 7d). Valores: SinLimitacion, Moderada o Alta. Dato de salud.</summary>
    public class LimitacionesArticularesDto
    {
        public string Rodilla { get; set; } = "SinLimitacion";
        public string Lumbar { get; set; } = "SinLimitacion";
    }

    /// <summary>
    /// KPIs de progresión del socio (Etapa 7d). Indicadores descriptivos, no predictivos ni causales. Con datos sintéticos,
    /// K1 y K2 reflejan el modelo del generador y no la fisiología.
    /// </summary>
    public class KpiProgresionDto
    {
        public Guid SocioId { get; set; }

        /// <summary>
        /// False si el socio no entra en el cálculo: solo se incluyen socios Sintetico (y ValidacionFuncional si se pidió
        /// expresamente, nunca para el estudio). En ese caso las listas quedan vacías y <see cref="Motivo"/> lo explica.
        /// </summary>
        public bool Incluido { get; set; }
        public string? Motivo { get; set; }
        public string OrigenDatos { get; set; } = string.Empty;
        public bool IncluyeValidacionFuncional { get; set; }
        public DateTime? Desde { get; set; }
        public DateTime? Hasta { get; set; }

        /// <summary>Sesiones de rutinas marcadas excluida_estudio que no se contaron.</summary>
        public int SesionesExcluidas { get; set; }

        public IReadOnlyList<DeltaCarga> K1DeltaCargaMaxima { get; set; } = Array.Empty<DeltaCarga>();
        public IReadOnlyList<VolumenSesion> K2VolumenEfectivo { get; set; } = Array.Empty<VolumenSesion>();
        public IReadOnlyList<AdherenciaEjercicio> K3Adherencia { get; set; } = Array.Empty<AdherenciaEjercicio>();
        public IndiceSeguridad? K4IndiceSeguridad { get; set; }
    }
}
