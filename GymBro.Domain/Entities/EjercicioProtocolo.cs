using GymBro.Domain.Enums;

namespace GymBro.Domain.Entities
{
    /// <summary>
    /// Fila del catálogo de un protocolo de entrenamiento (Etapa 7d, tabla ejercicios_protocolo). Catálogo global: no
    /// tiene filtro por gimnasio ni datos de salud. El protocolo de piernas usa solo los ejercicios listados acá (lista
    /// blanca); el resto del catálogo (por ejemplo, los ejercicios Legs del dataset) no participa.
    ///
    /// Los valores de grupo, rol, clase y las exclusiones por limitación son decisiones de diseño provisionales, sin
    /// fuente bibliográfica ni validación clínica; los revisa la Personal Trainer antes de usarse con personas reales.
    /// </summary>
    public class EjercicioProtocolo : EntidadAuditable
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>Código del protocolo (por ejemplo, PIERNAS).</summary>
        public string ProtocoloCodigo { get; set; } = string.Empty;

        /// <summary>Versión del protocolo (por ejemplo, piernas-1.0).</summary>
        public string ProtocoloVersion { get; set; } = string.Empty;

        public Guid EjercicioId { get; set; }

        public GrupoMuscularPiernas Grupo { get; set; }
        public RolEjercicioProtocolo Rol { get; set; }
        public ClaseEjercicio Clase { get; set; }

        /// <summary>Se excluye si la limitación de rodilla del socio es igual o mayor. Null: nunca se excluye por rodilla.</summary>
        public NivelLimitacion? RodillaExcluirDesde { get; set; }

        /// <summary>Se excluye si la limitación lumbar del socio es igual o mayor. Null: nunca se excluye por la zona lumbar.</summary>
        public NivelLimitacion? LumbarExcluirDesde { get; set; }

        /// <summary>Desempate fijo de la rotación de variables; en las referencias identifica la posición (1, 2...).</summary>
        public short OrdenRotacion { get; set; }
    }
}
