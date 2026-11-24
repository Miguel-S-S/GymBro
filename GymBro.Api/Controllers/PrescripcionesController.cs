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
        private readonly IPrescripcionPiernasService _piernas;

        public PrescripcionesController(IPrescripcionBaselineService baseline, IPrescripcionPiernasService piernas)
        {
            _baseline = baseline;
            _piernas = piernas;
        }

        /// <summary>
        /// Historial de rutinas del socio, la más reciente primero. Etapa 7d: incluye los campos aditivos del protocolo
        /// de piernas por línea (rol, grupo, tempo y carga sugerida); el resto del contenido no cambia.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Listar(Guid socioId) => Ok(await _piernas.ListarDeSocioAsync(socioId));

        /// <summary>Genera la rutina baseline determinista (4 a 6 ejercicios) a partir del perfil y la última medición.</summary>
        [HttpPost("baseline")]
        public async Task<IActionResult> GenerarBaseline(Guid socioId)
        {
            var rutina = await _baseline.GenerarAsync(socioId);
            return Created($"api/Socio/{socioId}/prescripciones", rutina);
        }

        /// <summary>
        /// Etapa 7d: genera la rutina de piernas determinista del protocolo (hasta 6 ejercicios de la lista curada), con
        /// la dosis según el perfil, el historial y las alertas vigentes. Parámetros provisionales, sin validación clínica.
        /// </summary>
        [HttpPost("piernas")]
        public async Task<IActionResult> GenerarPiernas(Guid socioId)
        {
            var rutina = await _piernas.GenerarAsync(socioId);
            return Created($"api/Socio/{socioId}/prescripciones", rutina);
        }
    }
}
