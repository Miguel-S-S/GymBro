using GymBro.Application.DTOs;

namespace GymBro.Application.Services
{
    /// <summary>
    /// Consulta del catálogo de ejercicios (solo lectura).
    /// Es independiente de la búsqueda semántica, que vive en IBuscadorSemanticoService.
    /// </summary>
    public interface IEjercicioCatalogoService
    {
        Task<PaginadoDto<EjercicioResumenDto>> GetCatalogoAsync(string? texto, string? zonaObjetivo, int pagina, int tamanioPagina);
        Task<EjercicioDetalleDto?> GetByIdAsync(Guid id);
        Task<IReadOnlyList<string>> GetZonasObjetivoAsync();
    }
}