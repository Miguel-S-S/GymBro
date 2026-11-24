using GymBro.Application.DTOs;
using GymBro.Application.Services;
using GymBro.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymBro.Api.Controllers
{
    /// <summary>
    /// KPIs de progresión del socio (Etapa 7d). Indicadores descriptivos sobre datos de salud: mismos roles y aislamiento
    /// que las demás lecturas del socio; el Administrador de la Plataforma no tiene acceso.
    /// </summary>
    [ApiController]
    [Route("api/Socio/{socioId:guid}/kpis-progresion")]
    [Authorize(Roles = Roles.AdministradorGimnasio + "," + Roles.Entrenador + "," + Roles.Socio)]
    public class KpisProgresionController : ControllerBase
    {
        private readonly IKpiProgresionService _kpis;
        public KpisProgresionController(IKpiProgresionService kpis) { _kpis = kpis; }

        /// <summary>K1 a K4 en el período (fechas UTC, opcionales). Por defecto solo datos sintéticos.</summary>
        [HttpGet]
        public async Task<ActionResult<KpiProgresionDto>> Calcular(Guid socioId, [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta,
            [FromQuery] bool incluirValidacionFuncional = false) =>
            Ok(await _kpis.CalcularAsync(socioId, desde, hasta, incluirValidacionFuncional));
    }
}
