using GymBro.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace GymBro.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EjerciciosController : ControllerBase
    {
        // Solo inyectamos la abstracción de la capa de Aplicación
        private readonly IBuscadorSemanticoService _buscadorSemantico;

        public EjerciciosController(IBuscadorSemanticoService buscadorSemantico)
        {
            _buscadorSemantico = buscadorSemantico;
        }

        [HttpGet("busqueda-inteligente")]
        public async Task<IActionResult> BuscarConIA([FromQuery] string consulta, [FromQuery] int cantidad = 5)
        {
            if (string.IsNullOrWhiteSpace(consulta))
                return BadRequest("La consulta no puede estar vacía.");

            var resultados = await _buscadorSemantico.BuscarEjerciciosSimilaresAsync(consulta, cantidad);

            if (resultados == null || !resultados.Any())
                return NotFound("No se encontraron ejercicios que coincidan con la búsqueda.");

            return Ok(resultados);
        }
    }
}