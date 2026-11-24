using GymBro.Domain.Enums;

namespace GymBro.Domain.Rules
{
    /// <summary>
    /// Parámetros del protocolo de piernas (Etapa 7d). Se leen de la sección de configuración "ProtocoloPiernas"; los
    /// valores por defecto viven acá (mismo patrón que ReglasSeguridadConfig).
    ///
    /// IMPORTANTE: todos los valores son criterios de diseño provisionales (decisión D5), sin fuente bibliográfica ni
    /// validación clínica. La Personal Trainer los revisa antes de cualquier uso con personas reales. La dosis NO sale de
    /// la tabla dosificaciones del dataset (sus filas de Legs tienen valores dudosos; ver el informe de la etapa).
    /// <see cref="Version"/> se incrementa cuando cambia cualquier valor o regla.
    /// </summary>
    public class ProtocoloPiernasConfig
    {
        public const string ProtocoloCodigo = "PIERNAS";
        public const string OrigenGeneracion = "PROTOCOLO_PIERNAS_DETERMINISTA";

        public string Version { get; set; } = "piernas-1.0";

        public int SeriesBaseMultiarticular { get; set; } = 3;
        public int SeriesBaseAislamiento { get; set; } = 2;
        public int SeriesBasePantorrilla { get; set; } = 3;
        public int SeriesMaxMultiarticular { get; set; } = 4;
        public int SeriesMaxAislamiento { get; set; } = 3;
        public int SeriesTotalesMaxSesion { get; set; } = 18;

        public RangoRepeticiones RepeticionesMultiarticular { get; set; } = new() { Minimas = 8, Maximas = 12 };
        public RangoRepeticiones RepeticionesAislamiento { get; set; } = new() { Minimas = 10, Maximas = 15 };
        public RangoRepeticiones RepeticionesPantorrilla { get; set; } = new() { Minimas = 15, Maximas = 20 };

        /// <summary>RIR objetivo por defecto. La generación automática nunca prescribe RIR 0.</summary>
        public int RirObjetivo { get; set; } = 2;
        /// <summary>RIR mínimo al que puede bajar (nunca menos de 1, aunque se configure 0).</summary>
        public int RirMinimo { get; set; } = 1;

        public int DescansoMultiarticularSeg { get; set; } = 150;
        /// <summary>Descanso de multiarticulares si la edad es mayor o igual a <see cref="EdadAjuste"/> o hay alguna limitación Moderada o Alta.</summary>
        public int DescansoMultiarticularAjustadoSeg { get; set; } = 180;
        public int DescansoAislamientoSeg { get; set; } = 75;

        public int TempoExcentricoSeg { get; set; } = 3;
        /// <summary>Tempo excéntrico con el mismo ajuste por edad o limitación que el descanso.</summary>
        public int TempoExcentricoAjustadoSeg { get; set; } = 4;

        /// <summary>Reducción de series por alerta de seguridad vigente, en porcentaje.</summary>
        public decimal ReduccionPorAlertaPorcentaje { get; set; } = 30m;
        /// <summary>Piso de series por ejercicio al reducir por alerta.</summary>
        public int SeriesMinimasConAlerta { get; set; } = 2;
        /// <summary>Días hacia atrás en los que una alerta (Tipo = Alerta, sin baja) cuenta como vigente.</summary>
        public int VentanaAlertasDias { get; set; } = 14;

        /// <summary>Adherencia mínima al rango de repeticiones (en %) del último registro para bajar a RIR 1 o sumar una serie.</summary>
        public decimal AdherenciaMinimaPorcentaje { get; set; } = 80m;

        public int EdadAjuste { get; set; } = 40;

        /// <summary>
        /// Posiciones de la rutina, en orden (propuesta P3). Si se deja vacía se usa <see cref="ComposicionPorDefecto"/>
        /// (mismo criterio que BaselineConfig.Zonas: el binder agrega a la lista, no la reemplaza).
        /// </summary>
        public List<PosicionProtocolo> Composicion { get; set; } = new();

        public IReadOnlyList<PosicionProtocolo> ComposicionEfectiva() => Composicion.Count > 0 ? Composicion : ComposicionPorDefecto;

        /// <summary>
        /// Seis posiciones: Cuádriceps-Referencia 1 (Prensa 45), Cuádriceps-Referencia 2 (Hack), Cuádriceps-Variable,
        /// Isquiotibiales-Referencia 1 (Curl sentado), Isquiotibiales-Variable y Pantorrillas-Variable. 16 series con los
        /// valores base.
        /// </summary>
        public static readonly IReadOnlyList<PosicionProtocolo> ComposicionPorDefecto = new List<PosicionProtocolo>
        {
            new() { Grupo = GrupoMuscularPiernas.Cuadriceps, Rol = RolEjercicioProtocolo.Referencia, OrdenReferencia = 1 },
            new() { Grupo = GrupoMuscularPiernas.Cuadriceps, Rol = RolEjercicioProtocolo.Referencia, OrdenReferencia = 2 },
            new() { Grupo = GrupoMuscularPiernas.Cuadriceps, Rol = RolEjercicioProtocolo.Variable },
            new() { Grupo = GrupoMuscularPiernas.Isquiotibiales, Rol = RolEjercicioProtocolo.Referencia, OrdenReferencia = 1 },
            new() { Grupo = GrupoMuscularPiernas.Isquiotibiales, Rol = RolEjercicioProtocolo.Variable },
            new() { Grupo = GrupoMuscularPiernas.Pantorrillas, Rol = RolEjercicioProtocolo.Variable },
        };
    }

    public class RangoRepeticiones
    {
        public int Minimas { get; set; }
        public int Maximas { get; set; }
    }

    /// <summary>
    /// Posición de la rutina. En una referencia, <see cref="OrdenReferencia"/> identifica el ejercicio fijo por su
    /// orden_rotacion dentro del grupo (así no se escriben UUID en la configuración). En una variable se ignora.
    /// </summary>
    public class PosicionProtocolo
    {
        public GrupoMuscularPiernas Grupo { get; set; }
        public RolEjercicioProtocolo Rol { get; set; }
        public int? OrdenReferencia { get; set; }
    }
}
