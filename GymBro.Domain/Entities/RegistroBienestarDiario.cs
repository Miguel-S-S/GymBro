using GymBro.Domain.Enums;

namespace GymBro.Domain.Entities
{
    /// <summary>
    /// Registro diario de bienestar autopercibido del socio (Etapa 8b): uno por socio y día local.
    /// Todas las variables son opcionales, pero el registro exige al menos una. Null = "sin dato".
    /// Se asocia a las sesiones por (SocioId, fecha local de SesionEntrenamiento.Inicio), sin clave foránea.
    /// </summary>
    public class RegistroBienestarDiario : EntidadAuditable
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid SocioId { get; set; }
        public Socio Socio { get; set; } = null!;

        /// <summary>Día local del socio (America/Argentina/Buenos_Aires), calculado en el servidor.</summary>
        public DateOnly Fecha { get; set; }

        public decimal? HorasSueno { get; set; }
        /// <summary>Estrés autopercibido, 1 (mejor) a 7 (peor), escala estilo Hooper.</summary>
        public short? Estres { get; set; }
        /// <summary>Fatiga autopercibida, 1 (mejor) a 7 (peor), escala estilo Hooper.</summary>
        public short? Fatiga { get; set; }
        public decimal? PesoKg { get; set; }
        public NutricionCategoria? NutricionCategoria { get; set; }
        public decimal? IngestaAguaLitros { get; set; }
        /// <summary>Texto libre del socio. No se analiza.</summary>
        public string? NutricionNotas { get; set; }
        public short? FrecuenciaCardiacaReposo { get; set; }

        public bool TieneAlgunDato() =>
            HorasSueno is not null || Estres is not null || Fatiga is not null || PesoKg is not null ||
            NutricionCategoria is not null || IngestaAguaLitros is not null ||
            !string.IsNullOrWhiteSpace(NutricionNotas) || FrecuenciaCardiacaReposo is not null;
    }

    /// <summary>Rangos válidos del registro de bienestar. Se aplican en el servicio y como CHECK en la base.</summary>
    public static class RangosBienestar
    {
        public const decimal HorasSuenoMin = 0m, HorasSuenoMax = 24m, HorasSuenoPaso = 0.5m;
        public const short EscalaMin = 1, EscalaMax = 7;
        /// <summary>Mismo rango que "Mi medición" (Etapa 8).</summary>
        public const decimal PesoMin = 1m, PesoMax = 400m;
        public const decimal AguaMin = 0m, AguaMax = 10m;
        public const int NotasMaxLargo = 500;
        /// <summary>
        /// Decisión de diseño de la Etapa 8b (no es un criterio clínico): admite la bradicardia de deportistas entrenados
        /// y valores por encima del rango habitual en reposo, y rechaza errores evidentes de tipeo (0, 9, 600).
        /// </summary>
        public const short FcReposoMin = 30, FcReposoMax = 120;
    }
}
