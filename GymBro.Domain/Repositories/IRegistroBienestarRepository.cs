using GymBro.Domain.Entities;

namespace GymBro.Domain.Repositories
{
    /// <summary>Registros diarios de bienestar (Etapa 8b). Las consultas pasan por el filtro de tenant.</summary>
    public interface IRegistroBienestarRepository
    {
        /// <summary>Registro vigente del socio para ese día, con seguimiento de cambios (para editarlo).</summary>
        Task<RegistroBienestarDiario?> GetDelDiaAsync(Guid socioId, DateOnly fecha);

        /// <summary>Registros del socio entre las dos fechas (inclusive), del más reciente al más antiguo.</summary>
        Task<IReadOnlyList<RegistroBienestarDiario>> ListarAsync(Guid socioId, DateOnly desde, DateOnly hasta);

        void Agregar(RegistroBienestarDiario registro);

        /// <summary>
        /// Persiste los cambios. Si otro pedido ya creó el registro del mismo día (índice único), lanza ReglaNegocioException.
        /// </summary>
        Task GuardarCambiosAsync();
    }
}
