using GymBro.Domain.Enums;

namespace GymBro.Domain.Entities
{
    /// <summary>
    /// Alerta o sugerencia generada por las reglas deterministas de seguridad (Etapa 10a). Es un dato de salud: la ven el
    /// propio socio y el personal de su gimnasio (el Personal Trainer, solo de sus socios asignados); el Administrador de
    /// la Plataforma no accede.
    ///
    /// Idempotencia: una sola fila por (socio, regla, clave de origen). Reevaluar actualiza la fila, la retira con baja
    /// lógica si la condición deja de cumplirse y la reactiva si vuelve a cumplirse.
    /// </summary>
    public class AlertaSeguridad : EntidadAuditable
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid SocioId { get; set; }
        public Socio Socio { get; set; } = null!;

        /// <summary>Gimnasio del socio al generarse; null para socios independientes. Informativo (el filtro usa el gimnasio actual del socio).</summary>
        public Guid? GimnasioId { get; set; }

        public Guid? SesionId { get; set; }
        public Guid? SesionEjercicioId { get; set; }
        public Guid? RegistroBienestarId { get; set; }

        /// <summary>R-RIR-1, R-PROG-1 o R-FC-1 (<see cref="CodigosRegla"/>).</summary>
        public string ReglaCodigo { get; set; } = string.Empty;

        /// <summary>Id del origen (ejercicio de la sesión o registro de bienestar), como texto. Parte de la clave única.</summary>
        public string ClaveOrigen { get; set; } = string.Empty;

        public TipoAlerta Tipo { get; set; }
        public SeveridadAlerta Severidad { get; set; }
        public string Mensaje { get; set; } = string.Empty;

        /// <summary>JSON (jsonb): entradas, umbrales aplicados, resultado y versión de las reglas.</summary>
        public string Detalle { get; set; } = "{}";

        public EstadoAlerta Estado { get; set; } = EstadoAlerta.Pendiente;
        public Guid? VistaPorUsuarioId { get; set; }
        public DateTime? VistaEn { get; set; }
    }
}
