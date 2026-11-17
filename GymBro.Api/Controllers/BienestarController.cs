using System.Globalization;
using GymBro.Application.DTOs.Bienestar;
using GymBro.Application.Services;
using GymBro.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymBro.Api.Controllers
{
    /// <summary>
    /// Registro diario de bienestar del socio (Etapa 8b). Datos de salud: el Administrador de la Plataforma no accede.
    /// Los errores los traduce ManejadorExcepciones (400/403/404; el 500 no expone el detalle).
    /// </summary>
    [ApiController]
    [Route("api/bienestar")]
    public class BienestarController : ControllerBase
    {
        private readonly IBienestarService _bienestar;
        public BienestarController(IBienestarService bienestar) { _bienestar = bienestar; }

        /// <summary>Día local del servidor y el registro de hoy del socio autenticado (registro null si no cargó).</summary>
        [HttpGet("hoy")]
        [Authorize(Roles = Roles.Socio)]
        public async Task<ActionResult<BienestarHoyDto>> Hoy() => Ok(await _bienestar.ObtenerHoyAsync());

        /// <summary>Alta o actualización del registro de hoy o de ayer (yyyy-MM-dd) del socio autenticado.</summary>
        [HttpPut("{fecha}")]
        [Authorize(Roles = Roles.Socio)]
        public async Task<ActionResult<RegistroBienestarDto>> Guardar(string fecha, [FromBody] RegistroBienestarRequest request)
        {
            if (!DateOnly.TryParseExact(fecha, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dia))
                throw new ArgumentException("La fecha debe tener el formato yyyy-MM-dd.");
            return Ok(await _bienestar.GuardarAsync(dia, request));
        }

        /// <summary>Historial de un socio (el propio, o los del gimnasio o asignados según el rol).</summary>
        [HttpGet]
        [Authorize(Roles = Roles.AdministradorGimnasio + "," + Roles.Entrenador + "," + Roles.Socio)]
        public async Task<ActionResult<IReadOnlyList<RegistroBienestarDto>>> Historial(
            [FromQuery] Guid socioId, [FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta) =>
            Ok(await _bienestar.ListarAsync(socioId, desde, hasta));
    }
}
