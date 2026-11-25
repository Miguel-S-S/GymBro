using GymBro.Application.DTOs;
using GymBro.Application.DTOs.Acceso;
using GymBro.Application.Services;
using GymBro.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymBro.Api.Controllers
{
    /// <summary>Revisión humana opcional de las rutinas generadas por la IA (Dueño o Personal Trainer asignado).</summary>
    [ApiController]
    [Route("api/revisiones")]
    [Authorize(Roles = Roles.PersonalGimnasio)]
    public class RevisionesController : ControllerBase
    {
        private readonly IRevisionPrescripcionService _revisiones;
        private readonly ISugerenciaSustitutosService _sustitutos;

        public RevisionesController(IRevisionPrescripcionService revisiones, ISugerenciaSustitutosService sustitutos)
        {
            _revisiones = revisiones;
            _sustitutos = sustitutos;
        }

        [HttpGet("pendientes")]
        public async Task<IActionResult> Pendientes() => Ok(await _revisiones.ListarPendientesAsync());

        [HttpPost("{prescripcionId:guid}")]
        public async Task<IActionResult> Revisar(Guid prescripcionId, [FromBody] RevisarRutinaRequest request) =>
Ok(await _revisiones.RevisarAsync(prescripcionId, request));

        /// <summary>
        /// Etapa 9b-2 (caso a): sugerencia de sustitutos de una línea de una rutina pendiente de revisión, elegidos por el
        /// modelo local solo entre los candidatos recuperados y verificados en código. No modifica la rutina ni llega al Socio.
        /// </summary>
        [HttpPost("{prescripcionId:guid}/lineas/{lineaId:guid}/sustitutos")]
        public async Task<ActionResult<SugerenciaSustitutosDto>> SugerirSustitutos(Guid prescripcionId, Guid lineaId,
            [FromBody] SugerirSustitutosRequest request) =>
            Ok(await _sustitutos.SugerirAsync(prescripcionId, lineaId, request));
    }
}
