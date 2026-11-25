using System.Globalization;
using GymBro.Application.DTOs.AsistenciaCuotas;
using GymBro.Application.Services;
using GymBro.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymBro.Api.Controllers
{
    /// <summary>
    /// Etapa 11: asistencia al gimnasio. El Socio obtiene su QR; el Dueño y el Personal Trainer registran (QR o manual);
    /// el Administrador de la Plataforma solo lee, entre gimnasios. No son datos de salud.
    /// </summary>
    [ApiController]
    [Route("api/asistencia")]
    public class AsistenciaController : ControllerBase
    {
        private readonly IAsistenciaService _asistencia;
        public AsistenciaController(IAsistenciaService asistencia) { _asistencia = asistencia; }

        /// <summary>Token firmado de corta duración para dibujar el QR del Socio autenticado.</summary>
        [HttpPost("qr")]
        [Authorize(Roles = Roles.Socio)]
        public async Task<ActionResult<QrAsistenciaDto>> GenerarQr() => Ok(await _asistencia.GenerarQrAsync());

        /// <summary>El personal valida el código (leído con la cámara o tipeado) y registra la asistencia del día.</summary>
        [HttpPost("qr/validar")]
        [Authorize(Roles = Roles.PersonalGimnasio)]
        public async Task<ActionResult<RegistroAsistenciaDto>> RegistrarPorQr([FromBody] RegistrarAsistenciaQrRequest request) =>
            Ok(await _asistencia.RegistrarPorQrAsync(request?.Token));

        /// <summary>El personal registra la asistencia de un socio de su gimnasio sin QR.</summary>
        [HttpPost("manual")]
        [Authorize(Roles = Roles.PersonalGimnasio)]
        public async Task<ActionResult<RegistroAsistenciaDto>> RegistrarManual([FromBody] RegistrarAsistenciaManualRequest request) =>
            Ok(await _asistencia.RegistrarManualAsync(request?.SocioId));

        /// <summary>Asistencias del día (yyyy-MM-dd; por defecto hoy). La plataforma puede omitir gimnasioId (todos).</summary>
        [HttpGet]
        [Authorize(Roles = Roles.SuperAdministrador + "," + Roles.PersonalGimnasio)]
        public async Task<ActionResult<AsistenciaDelDiaDto>> DelDia([FromQuery] string? fecha, [FromQuery] Guid? gimnasioId)
        {
            DateOnly? dia = null;
            if (!string.IsNullOrWhiteSpace(fecha))
            {
                if (!DateOnly.TryParseExact(fecha, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                    throw new ArgumentException("La fecha debe tener el formato yyyy-MM-dd.");
                dia = d;
            }
            return Ok(await _asistencia.ListarDelDiaAsync(dia, gimnasioId));
        }
    }

    /// <summary>
    /// Etapa 11: cuotas mensuales (sin pasarela de pago). El Dueño genera y registra pagos; la plataforma lee entre
    /// gimnasios; el Socio ve las suyas; el Personal Trainer no accede.
    /// </summary>
    [ApiController]
    [Route("api/cuotas")]
    public class CuotasController : ControllerBase
    {
        private readonly ICuotaService _cuotas;
        public CuotasController(ICuotaService cuotas) { _cuotas = cuotas; }

        [HttpPost("generar")]
        [Authorize(Roles = Roles.AdministradorGimnasio)]
        public async Task<ActionResult<GenerarCuotasResultadoDto>> Generar([FromBody] GenerarCuotasRequest request) =>
            Ok(await _cuotas.GenerarMesAsync(request));

        [HttpPut("{id:guid}/pago")]
        [Authorize(Roles = Roles.AdministradorGimnasio)]
        public async Task<ActionResult<CuotaDto>> RegistrarPago(Guid id, [FromBody] RegistrarPagoRequest? request) =>
            Ok(await _cuotas.RegistrarPagoAsync(id, request));

        /// <summary>Cuotas del período (año y mes). La plataforma puede omitir gimnasioId (todos).</summary>
        [HttpGet]
        [Authorize(Roles = Roles.SuperAdministrador + "," + Roles.AdministradorGimnasio)]
        public async Task<ActionResult<IReadOnlyList<CuotaDto>>> Periodo([FromQuery] int anio, [FromQuery] int mes, [FromQuery] Guid? gimnasioId) =>
            Ok(await _cuotas.ListarPeriodoAsync(anio, mes, gimnasioId));

        /// <summary>Cuotas vencidas (pendientes con vencimiento anterior a hoy).</summary>
        [HttpGet("morosos")]
        [Authorize(Roles = Roles.SuperAdministrador + "," + Roles.AdministradorGimnasio)]
        public async Task<ActionResult<IReadOnlyList<CuotaDto>>> Morosos([FromQuery] Guid? gimnasioId) =>
            Ok(await _cuotas.ListarMorososAsync(gimnasioId));

        [HttpGet("mias")]
        [Authorize(Roles = Roles.Socio)]
        public async Task<ActionResult<IReadOnlyList<CuotaDto>>> Mias() => Ok(await _cuotas.MisCuotasAsync());
    }
}
