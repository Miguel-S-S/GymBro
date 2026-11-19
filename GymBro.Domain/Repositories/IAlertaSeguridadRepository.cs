using GymBro.Domain.Entities;
using GymBro.Domain.Enums;

namespace GymBro.Domain.Repositories
{
    /// <summary>Datos que necesita el servicio de reglas de seguridad (Etapa 10a).</summary>
    public interface IAlertaSeguridadRepository
    {
        /// <summary>Alerta por clave natural, con seguimiento, incluidas las dadas de baja (para reactivarla). Uso interno.</summary>
        Task<AlertaSeguridad?> GetPorClaveAsync(Guid socioId, string reglaCodigo, string claveOrigen);

        /// <summary>Alertas vigentes de la clave indicada que ya no corresponden (para retirarlas). Uso interno.</summary>
        Task<IReadOnlyList<AlertaSeguridad>> GetVigentesDeOrigenesAsync(Guid socioId, string reglaCodigo, IReadOnlyCollection<string> clavesOrigen);

        /// <summary>Alerta visible para quien llama (filtro global), con seguimiento.</summary>
        Task<AlertaSeguridad?> GetVisibleAsync(Guid id);

        /// <summary>Alertas visibles (filtro global) del socio indicado o, sin socio, de los socios indicados; la más reciente primero.</summary>
        Task<IReadOnlyList<AlertaSeguridad>> ListarAsync(IReadOnlyCollection<Guid>? socios, EstadoAlerta? estado);

        /// <summary>Sesión con sus ejercicios, series, ejercicio del catálogo (tipo de medida) y la rutina con sus líneas.</summary>
        Task<SesionEntrenamiento?> GetSesionParaEvaluarAsync(Guid sesionId);

        /// <summary>Registro de bienestar visible para quien llama, con su socio.</summary>
        Task<RegistroBienestarDiario?> GetRegistroBienestarAsync(Guid registroId);

        /// <summary>Registros de bienestar del socio entre dos fechas (inclusive).</summary>
        Task<IReadOnlyList<RegistroBienestarDiario>> GetBienestarAsync(Guid socioId, DateOnly desde, DateOnly hasta);

        void Agregar(AlertaSeguridad alerta);

        /// <summary>Descarta cambios pendientes de una evaluación que falló, para no arrastrarlos a otro guardado.</summary>
        void DescartarCambios();

        Task GuardarCambiosAsync();
    }
}
