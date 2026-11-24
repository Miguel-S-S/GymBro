using GymBro.Application.DTOs;
using GymBro.Application.Services;
using GymBro.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymBro.Api.Controllers
{
    /// <summary>
    /// Limitaciones articulares del socio (Etapa 7d). Datos de salud: Dueño de Gimnasio, Personal Trainer asignado y el
    /// propio Socio (SocioAccessService). El Administrador de la Plataforma no tiene acceso.
    /// </summary>
    [ApiController]
    [Route("api/Socio/{socioId:guid}/limitaciones-articulares")]
    [Authorize(Roles = Roles.AdministradorGimnasio + "," + Roles.Entrenador + "," + Roles.Socio)]
    public class LimitacionesArticularesController : ControllerBase
    {
        private readonly ILimitacionesArticularesService _limitaciones;
        public LimitacionesArticularesController(ILimitacionesArticularesService limitaciones) { _limitaciones = limitaciones; }

        [HttpGet]
        public async Task<ActionResult<LimitacionesArticularesDto>> Obtener(Guid socioId) =>
            Ok(await _limitaciones.ObtenerAsync(socioId));

        /// <summary>Actualiza rodilla y lumbar (SinLimitacion, Moderada o Alta). Exige el consentimiento para datos de salud.</summary>
        [HttpPut]
        public async Task<ActionResult<LimitacionesArticularesDto>> Actualizar(Guid socioId, [FromBody] LimitacionesArticularesDto request) =>
            Ok(await _limitaciones.ActualizarAsync(socioId, request));
    }
}
