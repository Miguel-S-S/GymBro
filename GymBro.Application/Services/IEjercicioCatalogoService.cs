using GymBro.Application.DTOs;

namespace GymBro.Application.Services
{
    /// <summary>
    /// Consulta del catálogo de ejercicios (solo lectura).
    /// Es independiente de la búsqueda semántica, que vive en IBuscadorSemanticoService.
    /// </summary>
    public interface IEjercicioCatalogoService
    {
        /// <summary>
        /// Página del catálogo. Con <paramref name="inactivos"/> = true devuelve solo los desactivados que quien llama puede
        /// gestionar (Etapa 9a-2b): todos para el Administrador de la Plataforma, los propios del gimnasio para el Dueño y
        /// el Personal Trainer; el Socio recibe 403.
        /// </summary>
        Task<PaginadoDto<EjercicioResumenDto>> GetCatalogoAsync(string? texto, string? zonaObjetivo, int pagina, int tamanioPagina,
            string? origen = null, bool inactivos = false);
        Task<EjercicioDetalleDto?> GetByIdAsync(Guid id);
        Task<IReadOnlyList<string>> GetZonasObjetivoAsync();
    }
}