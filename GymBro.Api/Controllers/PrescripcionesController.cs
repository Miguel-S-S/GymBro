using GymBro.Application.Services;
using GymBro.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymBro.Api.Controllers
{
    /// <summary>
    /// Prescripciones de un socio. Generan y consultan: Dueño de Gimnasio, Personal Trainer asignado y el propio Socio.
    /// El Administrador de la Plataforma no tiene acceso (datos de salud).
    /// </summary>
    [ApiController]
    [Route("api/Socio/{socioId:guid}/prescripciones")]
    [Authorize(Roles = Roles.AdministradorGimnasio + "," + Roles.Entrenador + "," + Roles.Socio)]
    public class PrescripcionesController : ControllerBase
    {
        private readonly IPrescripcionBaselineService _baseline;

        public PrescripcionesController(IPrescripcionBaselineService baseline)
        {
            _baseline = baseline;
        }

        /// <summary>Historial de rutinas del socio, la más reciente primero.</summary>
        [HttpGet]
        public async Task<IActionResult> Listar(Guid socioId) => Ok(await _baseline.ListarDeSocioAsync(socioId));

        /// <summary>Genera la rutina baseline determinista (4 a 6 ejercicios) a partir del perfil y la última medición.</summary>
        [HttpPost("baseline")]
        public async Task<IActionResult> GenerarBaseline(Guid socioId)
        {
            var rutina = await _baseline.GenerarAsync(socioId);
            return Created($"api/Socio/{socioId}/prescripciones", rutina);
        }
    }
}
