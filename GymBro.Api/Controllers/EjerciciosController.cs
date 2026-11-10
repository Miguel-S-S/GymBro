using GymBro.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace GymBro.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EjerciciosController : ControllerBase
    {
        // Solo inyectamos abstracciones de la capa de Aplicación
        private readonly IBuscadorSemanticoService _buscadorSemantico;
        private readonly IEjercicioCatalogoService _catalogo;

        public EjerciciosController(IBuscadorSemanticoService buscadorSemantico, IEjercicioCatalogoService catalogo)
        {
            _buscadorSemantico = buscadorSemantico;
            _catalogo = catalogo;
        }

        [HttpGet]
        public async Task<IActionResult> GetCatalogo(
            [FromQuery] string? texto,
            [FromQuery] string? zona,
            [FromQuery] int pagina = 1,
            [FromQuery] int tamanioPagina = 20)
        {
            try
            {
                var resultado = await _catalogo.GetCatalogoAsync(texto, zona, pagina, tamanioPagina);
                return Ok(resultado);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var ejercicio = await _catalogo.GetByIdAsync(id);
            if (ejercicio == null)
            {
                return NotFound();
            }

            return Ok(ejercicio);
        }

        [HttpGet("zonas")]
        public async Task<IActionResult> GetZonas()
        {
            var zonas = await _catalogo.GetZonasObjetivoAsync();
            return Ok(zonas);
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
