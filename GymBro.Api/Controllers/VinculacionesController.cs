using GymBro.Application.DTOs.Acceso;
using GymBro.Application.Services;
using GymBro.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymBro.Api.Controllers
{
    [ApiController]
    [Route("api/vinculaciones")]
    public class VinculacionesController : ControllerBase
    {
        private const string Gestores = Roles.SuperAdministrador + "," + Roles.AdministradorGimnasio;

        private readonly IVinculacionService _vinculaciones;
        public VinculacionesController(IVinculacionService vinculaciones) { _vinculaciones = vinculaciones; }

        /// <summary>El socio pide unirse a un gimnasio con su código.</summary>
        [HttpPost]
        [Authorize(Roles = Roles.Socio)]
        public async Task<IActionResult> Solicitar([FromBody] SolicitarVinculacionRequest request) =>
StatusCode(StatusCodes.Status201Created, await _vinculaciones.SolicitarAsync(request.CodigoGimnasio));

        [HttpGet("mias")]
        [Authorize(Roles = Roles.Socio)]
        public async Task<IActionResult> Mias() => Ok(await _vinculaciones.ListarMisSolicitudesAsync());

        /// <summary>
        /// Etapa 9a-2c (D1): también el Administrador de la Plataforma, indicando gimnasioId (sin él, 400). El Dueño opera
        /// sobre su gimnasio; un gimnasioId ajeno responde 404. Las solicitudes no contienen datos de salud.
        /// </summary>
        [HttpGet("pendientes")]
        [Authorize(Roles = Gestores)]
        public async Task<IActionResult> Pendientes([FromQuery] Guid? gimnasioId) => Ok(await _vinculaciones.ListarPendientesAsync(gimnasioId));

        /// <summary>Etapa 9a-2c (D1): aprobar o rechazar; el Administrador de la Plataforma indica gimnasioId en el cuerpo.</summary>
        [HttpPost("{id:guid}/resolver")]
        [Authorize(Roles = Gestores)]
        public async Task<IActionResult> Resolver(Guid id, [FromBody] ResolverSolicitudRequest request) =>
            Ok(await _vinculaciones.ResolverAsync(id, request.Aprobar, request.Motivo, request.GimnasioId));
    }
}