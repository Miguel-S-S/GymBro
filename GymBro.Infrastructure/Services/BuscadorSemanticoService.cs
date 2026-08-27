using GymBro.Application.DTOs;
using GymBro.Application.Services;
using GymBro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace GymBro.Infrastructure.Services
{
    public class BuscadorSemanticoService : IBuscadorSemanticoService
    {
        private readonly GymBroDbContext _context;
        private readonly IIaService _iaService;

        public BuscadorSemanticoService(GymBroDbContext context, IIaService iaService)
        {
            _context = context;
            _iaService = iaService;
        }

        public async Task<List<EjercicioBuscadoDto>> BuscarEjerciciosSimilaresAsync(string requerimientoUsuario, int cantidad = 5)
        {
            float[] embeddingArray = await _iaService.GenerarEmbeddingAsync(requerimientoUsuario);

            if (embeddingArray == null || embeddingArray.Length == 0)
                throw new Exception("El servicio de IA no devolvió un vector válido.");

            var vectorBuscado = new Vector(embeddingArray);

            return await _context.Ejercicios
                .Where(e => e.Embedding != null)
                .OrderBy(e => e.Embedding!.CosineDistance(vectorBuscado))
                .Take(cantidad)
                .Select(e => new EjercicioBuscadoDto
                {
                    Id = e.Id,
                    Nombre = e.Nombre,
                    ZonaObjetivo = e.ZonaObjetivo,
                    MusculoPrincipal = e.MusculoPrincipal,
                    Equipamiento = e.Equipamiento
                })
                .ToListAsync();
        }
    }
}