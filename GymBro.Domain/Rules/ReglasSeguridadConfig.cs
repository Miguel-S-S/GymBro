namespace GymBro.Domain.Rules
{
    /// <summary>
    /// Umbrales de las reglas deterministas de seguridad (Etapa 10a). Se leen de la sección de configuración
    /// "ReglasSeguridad"; los valores por defecto viven acá (mismo patrón que BaselineConfig).
    ///
    /// IMPORTANTE: todos los valores son decisiones de diseño provisionales, sin fuente bibliográfica. No están validados
    /// clínicamente y se revisan con la Personal Trainer antes de usarse con personas reales (decisión D4 del autor).
    /// </summary>
    public class ReglasSeguridadConfig
    {
        /// <summary>Versión de las reglas que se guarda en cada alerta. Se cambia cuando cambia la lógica de una regla.</summary>
        public const string ReglasVersion = "10a-1";

        public ConfigRir Rir { get; set; } = new();
        public ConfigProgresion Progresion { get; set; } = new();
        public ConfigFcReposo FcReposo { get; set; } = new();
        public LimitesPrescripcion Limites { get; set; } = new();

        /// <summary>R-RIR-1: esfuerzo mayor al planificado.</summary>
        public class ConfigRir
        {
            /// <summary>Alerta si RIR objetivo menos la mediana del RIR realizado es mayor o igual a este valor.</summary>
            public decimal DiferenciaMinima { get; set; } = 2m;
            /// <summary>Series de trabajo con RIR registrado que hacen falta para evaluar.</summary>
            public int SeriesMinimasConDato { get; set; } = 2;
        }

        /// <summary>R-PROG-1: sugerencia de carga (doble progresión simple).</summary>
        public class ConfigProgresion
        {
            public decimal IncrementoPorcentaje { get; set; } = 2.5m;
            public decimal DecrementoPorcentaje { get; set; } = 5m;
            /// <summary>La carga sugerida se redondea a múltiplos de este valor (al más cercano; los puntos medios hacia arriba).</summary>
            public decimal RedondeoKg { get; set; } = 0.5m;
            public decimal CambioMinimoKg { get; set; } = 0.5m;
            /// <summary>Tope del cambio respecto de la carga actual, en ambos sentidos. Si el cambio mínimo lo supera, no se sugiere.</summary>
            public decimal TopePorcentaje { get; set; } = 10m;
        }

        /// <summary>R-FC-1: frecuencia cardíaca en reposo elevada respecto de la propia línea base.</summary>
        public class ConfigFcReposo
        {
            /// <summary>Días anteriores que forman la línea base (no incluye el día evaluado).</summary>
            public int DiasLineaBase { get; set; } = 14;
            public int RegistrosMinimos { get; set; } = 5;
            /// <summary>Alerta si el valor del día supera la línea base en este porcentaje Y en <see cref="UmbralLpm"/>.</summary>
            public decimal UmbralPorcentaje { get; set; } = 10m;
            public int UmbralLpm { get; set; } = 5;
        }
    }

    /// <summary>R-LIM: límites duros de una prescripción (decisiones de diseño provisionales).</summary>
    public class LimitesPrescripcion
    {
        public int SeriesMin { get; set; } = 1;
        public int SeriesMax { get; set; } = 6;
        public int RepeticionesMin { get; set; } = 1;
        public int RepeticionesMax { get; set; } = 30;
        public int SegundosMin { get; set; } = 5;
        public int SegundosMax { get; set; } = 180;
        public int DescansoMinSegundos { get; set; } = 30;
        public int DescansoMaxSegundos { get; set; } = 600;
        public int RirMin { get; set; } = 0;
        public int RirMax { get; set; } = 5;
        public int RirMinPrincipiante { get; set; } = 2;
        public int EjerciciosMin { get; set; } = 1;
        public int EjerciciosMax { get; set; } = 12;
        public int SeriesTotalesMax { get; set; } = 30;
    }
}
