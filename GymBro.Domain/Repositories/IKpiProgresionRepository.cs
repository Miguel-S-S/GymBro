using GymBro.Domain.Entities;
using GymBro.Domain.Enums;

namespace GymBro.Domain.Repositories
{
    /// <summary>Lecturas de los KPIs de progresión (Etapa 7d). Sin seguimiento; las filas con baja lógica no se devuelven.</summary>
    public interface IKpiProgresionRepository
    {
        Task<Socio?> GetSocioAsync(Guid socioId);

        /// <summary>
        /// Sesiones del socio con inicio en el rango (extremos opcionales e inclusivos), con sus ejercicios, series y la
        /// rutina con sus líneas. El tipo de medida del ejercicio se completa aunque el ejercicio esté desactivado.
        /// </summary>
        Task<IReadOnlyList<SesionEntrenamiento>> GetSesionesAsync(Guid socioId, DateTime? desdeUtc, DateTime? hastaUtc);

        /// <summary>Grupo del protocolo de cada ejercicio (filas activas del código indicado; si hay varias versiones, la mayor).</summary>
        Task<IReadOnlyDictionary<Guid, GrupoMuscularPiernas>> GetGruposProtocoloAsync(string protocoloCodigo);

        /// <summary>Alertas y sugerencias vigentes (sin baja) del socio.</summary>
        Task<IReadOnlyList<AlertaSeguridad>> GetAlertasVigentesAsync(Guid socioId);

        /// <summary>Id y fecha local de los registros de bienestar del socio.</summary>
        Task<IReadOnlyDictionary<Guid, DateOnly>> GetFechasBienestarAsync(Guid socioId);

        /// <summary>Rutinas del protocolo del socio con alta en el rango, con el motivo de selección de sus líneas.</summary>
        Task<IReadOnlyList<PrescripcionEntrenamiento>> GetPrescripcionesProtocoloAsync(Guid socioId, string protocoloCodigo,
            DateTime? desdeUtc, DateTime? hastaUtc);
    }
}
