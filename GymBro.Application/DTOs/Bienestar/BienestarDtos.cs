namespace GymBro.Application.DTOs.Bienestar
{
    /// <summary>Cuerpo de PUT /api/bienestar/{fecha}. Todo opcional, pero se exige al menos un dato.</summary>
    public class RegistroBienestarRequest
    {
        public decimal? HorasSueno { get; set; }
        /// <summary>1 (mejor) a 7 (peor).</summary>
        public short? Estres { get; set; }
        /// <summary>1 (mejor) a 7 (peor).</summary>
        public short? Fatiga { get; set; }
        public decimal? PesoKg { get; set; }
        /// <summary>"Baja", "Habitual" o "Alta" respecto de lo habitual.</summary>
        public string? NutricionCategoria { get; set; }
        public decimal? IngestaAguaLitros { get; set; }
        public string? NutricionNotas { get; set; }
        public short? FrecuenciaCardiacaReposo { get; set; }
    }

    public class RegistroBienestarDto
    {
        public Guid Id { get; set; }
        public Guid SocioId { get; set; }
        /// <summary>Día local del socio (yyyy-MM-dd).</summary>
        public DateOnly Fecha { get; set; }
        public decimal? HorasSueno { get; set; }
        public short? Estres { get; set; }
        public short? Fatiga { get; set; }
        public decimal? PesoKg { get; set; }
        public string? NutricionCategoria { get; set; }
        public decimal? IngestaAguaLitros { get; set; }
        public string? NutricionNotas { get; set; }
        public short? FrecuenciaCardiacaReposo { get; set; }
        public DateTime FechaAlta { get; set; }
        public DateTime? FechaModificacion { get; set; }
    }

    /// <summary>Respuesta de GET /api/bienestar/hoy: día local del servidor y el registro de ese día (null si no hay).</summary>
    public class BienestarHoyDto
    {
        public DateOnly FechaLocal { get; set; }
        public RegistroBienestarDto? Registro { get; set; }
    }
}
