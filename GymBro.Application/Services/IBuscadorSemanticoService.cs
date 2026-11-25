using GymBro.Application.DTOs;

namespace GymBro.Application.Services
{
    public interface IBuscadorSemanticoService
    {
        /// <summary>
        /// Firma anterior (GET con la consulta en la URL), conservada por compatibilidad. Desde la Etapa 9b-1 aplica los
        /// mismos filtros duros que <see cref="BuscarAsync"/> con el gimnasio de quien consulta.
        /// </summary>
        Task<List<EjercicioBuscadoDto>> BuscarEjerciciosSimilaresAsync(string requerimientoUsuario, int cantidad = 5);

        /// <summary>
        /// Etapa 9b-1: busqueda por similitud coseno con filtros duros (activos, del gimnasio o globales, con equipamiento
        /// disponible en el gimnasio, zona y equipamiento opcionales).
        /// </summary>
        Task<List<EjercicioBuscadoDto>> BuscarAsync(BusquedaSemanticaRequest request);
    }
}
