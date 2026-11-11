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

        [HttpGet("pendientes")]
        [Authorize(Roles = Roles.AdministradorGimnasio)]
        public async Task<IActionResult> Pendientes() => Ok(await _vinculaciones.ListarPendientesAsync());

        [HttpPost("{id:guid}/resolver")]
        [Authorize(Roles = Roles.AdministradorGimnasio)]
        public async Task<IActionResult> Resolver(Guid id, [FromBody] ResolverSolicitudRequest request) =>
Ok(await _vinculaciones.ResolverAsync(id, request.Aprobar, request.Motivo));
    }
}