using GymBro.Application.DTOs;
using GymBro.Application.Services;
using GymBro.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
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
        private readonly IEjercicioGimnasioService _propios;

        public EjerciciosController(IBuscadorSemanticoService buscadorSemantico, IEjercicioCatalogoService catalogo,
            IEjercicioGimnasioService propios)
        {
            _buscadorSemantico = buscadorSemantico;
            _catalogo = catalogo;
            _propios = propios;
        }

        [HttpGet]
        public async Task<IActionResult> GetCatalogo(
            [FromQuery] string? texto,
            [FromQuery] string? zona,
            [FromQuery] int pagina = 1,
            [FromQuery] int tamanioPagina = 20,
            [FromQuery] string? origen = null)
        {
            try
            {
                var resultado = await _catalogo.GetCatalogoAsync(texto, zona, pagina, tamanioPagina, origen);
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

        // ---- Etapa 9a-2: ejercicios propios del gimnasio (solo el Dueño; el gimnasio sale del JWT) ----

        [HttpPost]
        [Authorize(Roles = Roles.AdministradorGimnasio)]
        public async Task<IActionResult> Crear([FromBody] EjercicioGimnasioRequest request)
        {
            var creado = await _propios.CrearAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = creado.Id }, creado);
        }

        [HttpPut("{id:guid}")]
        [Authorize(Roles = Roles.AdministradorGimnasio)]
        public async Task<IActionResult> Editar(Guid id, [FromBody] EjercicioGimnasioRequest request) =>
            Ok(await _propios.EditarAsync(id, request));

        /// <summary>Baja lógica. Rechazada si una rutina vigente o pendiente de revisión usa el ejercicio.</summary>
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = Roles.AdministradorGimnasio)]
        public async Task<IActionResult> DarDeBaja(Guid id)
        {
            await _propios.DarDeBajaAsync(id);
            return NoContent();
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
