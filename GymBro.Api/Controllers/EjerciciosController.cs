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
        /// <summary>
        /// Etapa 9a-2b: escriben el catálogo propio el Administrador de la Plataforma (cualquier gimnasio, indicado en el
        /// cuerpo), el Dueño y el Personal Trainer (su gimnasio). Los permisos finos (el Personal Trainer solo modifica lo
        /// suyo; los globales solo los desactiva la plataforma) los aplica EjercicioGimnasioService.
        /// </summary>
        private const string Gestores = Roles.SuperAdministrador + "," + Roles.PersonalGimnasio;

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
            [FromQuery] string? origen = null,
            [FromQuery] bool inactivos = false)
        {
            try
            {
                // inactivos=true: solo los desactivados que quien llama puede gestionar (Etapa 9a-2b; el Socio recibe 403).
                var resultado = await _catalogo.GetCatalogoAsync(texto, zona, pagina, tamanioPagina, origen, inactivos);
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

        // ---- Etapa 9a-2 y 9a-2b: ejercicios propios del gimnasio ----

        [HttpPost]
        [Authorize(Roles = Gestores)]
        public async Task<IActionResult> Crear([FromBody] EjercicioGimnasioRequest request)
        {
            var creado = await _propios.CrearAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = creado.Id }, creado);
        }

        [HttpPut("{id:guid}")]
        [Authorize(Roles = Gestores)]
        public async Task<IActionResult> Editar(Guid id, [FromBody] EjercicioGimnasioRequest request) =>
            Ok(await _propios.EditarAsync(id, request));

        /// <summary>
        /// Etapa 9a-2b: desactivar (activo = false) o reactivar (true). Reemplaza a la baja de la 9a-2: los ejercicios no se
        /// dan de baja. Desactivar se rechaza si una rutina vigente o pendiente de revisión usa el ejercicio.
        /// </summary>
        [HttpPut("{id:guid}/estado")]
        [Authorize(Roles = Gestores)]
        public async Task<IActionResult> CambiarEstado(Guid id, [FromBody] EstadoEjercicioRequest request) =>
            Ok(await _propios.CambiarEstadoAsync(id, request.Activo));

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
