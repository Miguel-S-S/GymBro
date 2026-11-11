using GymBro.Application.DTOs.Acceso;
using Microsoft.AspNetCore.Mvc;
using GymBro.Application.Services;
using GymBro.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymBro.Api.Controllers
{
    [ApiController]
    [Route("api/personal")]
    [Authorize(Roles = Roles.AdministradorGimnasio)]
    public class PersonalController : ControllerBase
    {
        private readonly IPersonalService _personal;
        public PersonalController(IPersonalService personal) { _personal = personal; }

        [HttpGet("entrenadores")]
        public async Task<IActionResult> GetEntrenadores() => Ok(await _personal.ListarEntrenadoresAsync());

        [HttpPost("entrenadores")]
        public async Task<IActionResult> PostEntrenador([FromBody] EntrenadorCreateDto dto) =>
StatusCode(StatusCodes.Status201Created, await _personal.CrearEntrenadorAsync(dto));

        [HttpPost("entrenadores/{entrenadorId:guid}/socios")]
        public async Task<IActionResult> AsignarSocio(Guid entrenadorId, [FromBody] AsignacionRequest request)
        {
            await _personal.AsignarSocioAsync(entrenadorId, request.SocioId);
            return NoContent();
        }
    }
}