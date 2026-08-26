using GymBro.Application.DTOs;
using GymBro.Application.Services;
using GymBro.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore; 

namespace GymBro.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EjerciciosController : ControllerBase
    {
        private readonly GymBroDbContext _context;
        private readonly IIaService _iaService;

        public EjerciciosController(GymBroDbContext context, IIaService iaService)
        {
            _context = context;
            _iaService = iaService;
        }

        [HttpGet("busqueda-inteligente")]
        public async Task<IActionResult> BuscarConIA([FromQuery] string consulta, [FromQuery] int cantidad = 5)
        {
            if (string.IsNullOrWhiteSpace(consulta))
                return BadRequest("La consulta no puede estar vacía.");

            // 1. Convertimos la frase del usuario a un vector usando Ollama (Python)
            var vectorConsultaArray = await _iaService.GenerarEmbeddingAsync(consulta);
            var vectorBuscado = new Vector(vectorConsultaArray);

            // 2. Buscamos en PostgreSQL los ejercicios matemáticamente más cercanos (Distancia de Cosenos)
            var resultados = await _context.Ejercicios
                .OrderBy(e => e.Embedding!.CosineDistance(vectorBuscado))
                .Take(cantidad)
                .Select(e => new EjercicioBuscadoDto
                {
                    Id = e.Id,
                    Nombre = e.Nombre,
                    GrupoMuscular = e.GrupoMuscular,
                    Equipamiento = e.Equipamiento
                })
                .ToListAsync();

            return Ok(resultados);
        }
    }
}