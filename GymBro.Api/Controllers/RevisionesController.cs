using GymBro.Application.DTOs.Acceso;
using Microsoft.AspNetCore.Mvc;
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
        public RevisionesController(IRevisionPrescripcionService revisiones) { _revisiones = revisiones; }

        [HttpGet("pendientes")]
        public async Task<IActionResult> Pendientes() => Ok(await _revisiones.ListarPendientesAsync());

        [HttpPost("{prescripcionId:guid}")]
        public async Task<IActionResult> Revisar(Guid prescripcionId, [FromBody] RevisarRutinaRequest request) =>
Ok(await _revisiones.RevisarAsync(prescripcionId, request));
    }
}