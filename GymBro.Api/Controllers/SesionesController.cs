using System;
using System.Threading.Tasks;
using GymBro.Application.DTOs;
using GymBro.Application.Services;
using GymBro.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymBro.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = Roles.AdministradorGimnasio + "," + Roles.Entrenador + "," + Roles.Socio)]
    public class SesionesController : ControllerBase
    {
        private readonly ISesionEntrenamientoService _sesionService;

        public SesionesController(ISesionEntrenamientoService sesionService)
        {
            _sesionService = sesionService;
        }

        [HttpPost]
        public async Task<IActionResult> CreateSesion([FromBody] RegistrarSesionEntrenamientoRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Los errores los traduce ManejadorExcepciones: ArgumentException y ReglaNegocioException -> 400,
            // AccesoDenegadoException -> 403 y cualquier otro -> 500 sin exponer el detalle interno.
            var result = await _sesionService.RegistrarSesionAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var sesion = await _sesionService.GetByIdAsync(id);
            if (sesion == null)
            {
                return NotFound();
            }

            return Ok(sesion);
        }

        [HttpGet("{id:guid}/comparacion")]
        public async Task<IActionResult> GetComparacion(Guid id)
        {
            var comparacion = await _sesionService.GetComparacionAsync(id);
            if (comparacion == null)
            {
                return NotFound();
            }

            return Ok(comparacion);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllBySocioId([FromQuery] Guid socioId)
        {
            if (socioId == Guid.Empty)
            {
                return BadRequest("Debe especificar un socioId válido.");
            }

            var sesiones = await _sesionService.GetAllBySocioIdAsync(socioId);
            return Ok(sesiones);
        }
    }
}
