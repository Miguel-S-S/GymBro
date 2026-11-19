using GymBro.Domain.Entities;

namespace GymBro.Domain.Repositories
{
    public interface IVinculacionRepository
    {
        Task<SolicitudVinculacion?> GetByIdAsync(Guid id);
        Task<IEnumerable<SolicitudVinculacion>> GetPendientesDeGimnasioAsync(Guid gimnasioId);

        /// <summary>
        /// Etapa 9a-2c (P1): lectura para el Administrador de la Plataforma, que no tiene gimnasio en el JWT y por eso no
        /// pasa el filtro global. Se ignora el filtro y se aplica uno explícito por el gimnasio indicado (y baja lógica).
        /// </summary>
        Task<IEnumerable<SolicitudVinculacion>> GetPendientesDeGimnasioExplicitoAsync(Guid gimnasioId);

        /// <summary>Etapa 9a-2c (P1): solicitud del gimnasio indicado, con filtro explícito; null si es de otro gimnasio.</summary>
        Task<SolicitudVinculacion?> GetEnGimnasioExplicitoAsync(Guid id, Guid gimnasioId);
        Task<IEnumerable<SolicitudVinculacion>> GetDeSocioAsync(Guid socioId);
        Task<bool> ExistePendienteAsync(Guid socioId, Guid gimnasioId);
        Task AddAsync(SolicitudVinculacion solicitud);
        Task SaveChangesAsync();
    }
}