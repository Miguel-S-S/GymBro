namespace GymBro.Application.DTOs.AsistenciaCuotas
{
    /// <summary>Token del QR del Socio (Etapa 11). El cliente lo dibuja; el personal lo valida.</summary>
    public class QrAsistenciaDto
    {
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiraUtc { get; set; }
        public int DuracionSegundos { get; set; }
    }

    /// <summary>Cuerpo de POST /api/asistencia/qr/validar.</summary>
    public class RegistrarAsistenciaQrRequest
    {
        public string? Token { get; set; }
    }

    /// <summary>Cuerpo de POST /api/asistencia/manual.</summary>
    public class RegistrarAsistenciaManualRequest
    {
        public Guid? SocioId { get; set; }
    }

    public class AsistenciaDto
    {
        public Guid Id { get; set; }
        public Guid SocioId { get; set; }
        public string SocioNombre { get; set; } = string.Empty;
        public Guid GimnasioId { get; set; }
        public string GimnasioNombre { get; set; } = string.Empty;
        public DateTime Instante { get; set; }
        public DateOnly Fecha { get; set; }
        /// <summary>"Qr" o "Manual".</summary>
        public string Metodo { get; set; } = string.Empty;
        public Guid RegistradoPorUsuarioId { get; set; }
    }

    /// <summary>Resultado del registro: idempotente por socio y dia local.</summary>
    public class RegistroAsistenciaDto
    {
        /// <summary>true si el socio ya tenia la asistencia del dia: no se crea otra fila.</summary>
        public bool YaRegistrada { get; set; }
        public AsistenciaDto Asistencia { get; set; } = new();
    }

    public class AsistenciaDelDiaDto
    {
        public DateOnly Fecha { get; set; }
        public int Total { get; set; }
        public IReadOnlyList<AsistenciaDto> Asistencias { get; set; } = Array.Empty<AsistenciaDto>();
    }

    /// <summary>Cuerpo de POST /api/cuotas/generar.</summary>
    public class GenerarCuotasRequest
    {
        public int Anio { get; set; }
        public int Mes { get; set; }
        public decimal Monto { get; set; }
        public DateOnly? Vencimiento { get; set; }
    }

    public class GenerarCuotasResultadoDto
    {
        public int Creadas { get; set; }
        /// <summary>Socios activos que ya tenian cuota en el periodo (no se duplican).</summary>
        public int Existentes { get; set; }
    }

    /// <summary>Cuerpo de PUT /api/cuotas/{id}/pago. Sin fecha, se usa el dia local de hoy.</summary>
    public class RegistrarPagoRequest
    {
        public DateOnly? FechaPago { get; set; }
    }

    public class CuotaDto
    {
        public Guid Id { get; set; }
        public Guid SocioId { get; set; }
        public string SocioNombre { get; set; } = string.Empty;
        public Guid GimnasioId { get; set; }
        public string GimnasioNombre { get; set; } = string.Empty;
        public int Anio { get; set; }
        public int Mes { get; set; }
        public decimal Monto { get; set; }
        public DateOnly Vencimiento { get; set; }
        public DateOnly? FechaPago { get; set; }
        /// <summary>Estado guardado: "Pendiente" o "Pagada".</summary>
        public string Estado { get; set; } = string.Empty;
        /// <summary>Derivado: pendiente con vencimiento anterior a hoy (no es un estado guardado).</summary>
        public bool Vencida { get; set; }
        public int DiasVencida { get; set; }
    }
}
