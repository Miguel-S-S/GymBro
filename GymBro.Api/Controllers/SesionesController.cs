using System;
using System.Threading.Tasks;
using GymBro.Application.DTOs;
using GymBro.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace GymBro.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
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

            try
            {
                var result = await _sesionService.RegistrarSesionAsync(request);
                return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "Error interno del servidor al procesar la sesión.", Detalle = ex.Message });
            }
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
