using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Domain.Rules;

namespace GymBro.Domain.Repositories
{
    /// <summary>Fila del protocolo con los datos del ejercicio que necesita el planificador (sin el embedding).</summary>
    public sealed record FilaProtocolo(Guid EjercicioId, string Nombre, TipoMedida TipoMedida, Guid? TipoEquipamientoId,
        GrupoMuscularPiernas Grupo, RolEjercicioProtocolo Rol, ClaseEjercicio Clase, NivelLimitacion? RodillaExcluirDesde,
        NivelLimitacion? LumbarExcluirDesde, short OrdenRotacion);

    /// <summary>
    /// Lecturas y escritura de la generación del protocolo de piernas (Etapa 7d). Consultas sin seguimiento y sin el
    /// vector de embedding. Orden fijo en todas las listas: no hay orden aleatorio.
    /// </summary>
    public interface IProtocoloPiernasRepository
    {
        /// <summary>Socio sin tracking (el filtro de tenant limita la consulta).</summary>
        Task<Socio?> GetSocioAsync(Guid socioId);

        Task<MedicionFisica?> GetUltimaMedicionAsync(Guid socioId);

        Task<bool> TienePendienteDeRevisionAsync(Guid socioId);

        /// <summary>Filas activas del protocolo y versión, con su ejercicio activo y visible (orden: grupo, rol, orden, id).</summary>
        Task<IReadOnlyList<FilaProtocolo>> GetFilasProtocoloAsync(string protocoloCodigo, string version);

        /// <summary>Mismo criterio que el baseline: tipos marcados más siempre disponibles; null si el gimnasio no marcó ninguno.</summary>
        Task<IReadOnlySet<Guid>?> GetTiposEquipamientoFactiblesAsync(Guid gimnasioId);

        /// <summary>
        /// Último registro del socio de cada ejercicio indicado (sesión más reciente por inicio y luego id), con las series
        /// sin baja y la línea de prescripción usada: el vínculo directo o, si es nulo, la línea de la rutina de la sesión
        /// con el mismo ejercicio (y el mismo orden si hay más de una).
        /// </summary>
        Task<IReadOnlyList<HistorialEjercicio>> GetUltimosRegistrosAsync(Guid socioId, IReadOnlyCollection<Guid> ejercicioIds);

        /// <summary>Ids de las alertas vigentes del socio: Tipo = Alerta, sin baja, en cualquier estado, creadas desde la fecha.</summary>
        Task<IReadOnlyList<Guid>> GetAlertasVigentesAsync(Guid socioId, DateTime desdeUtc);

        /// <summary>Mayor número de generación del socio en el protocolo (incluye rutinas dadas de baja); 0 si no hay.</summary>
        Task<int> GetMaxNumeroGeneracionAsync(Guid socioId, string protocoloCodigo);

        Task AddPrescripcionAsync(PrescripcionEntrenamiento prescripcion);

        /// <summary>Prescripciones del socio con sus líneas y el nombre y tipo de medida del ejercicio, la más reciente primero.</summary>
        Task<IReadOnlyList<PrescripcionEntrenamiento>> GetPrescripcionesDeSocioAsync(Guid socioId);

        Task SaveChangesAsync();
    }
}
