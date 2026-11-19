using GymBro.Domain.Enums;

namespace GymBro.Domain.Rules
{
    // Etapa 10a: reglas deterministas de seguridad. Funciones puras, sin acceso a datos: reciben los valores y los
    // umbrales (ReglasSeguridadConfig) y devuelven el resultado. No usan aprendizaje automático. Los umbrales son
    // decisiones de diseño provisionales, sin fuente bibliográfica, y las reglas no están validadas clínicamente.

    /// <summary>Serie realizada, con lo que necesitan las reglas.</summary>
    public sealed record SerieEvaluada(decimal PesoKg, int Repeticiones, int? Rir, bool EsCalentamiento);

    /// <summary>Lo planificado para un ejercicio en la prescripción.</summary>
    public sealed record ObjetivoEjercicio(int SeriesObjetivo, int RepeticionesMinimas, int RepeticionesMaximas, int? RirObjetivo,
        TipoMedida TipoMedida);

    public static class Estadistica
    {
        /// <summary>Mediana; con cantidad par, el promedio de los dos valores centrales. Null si no hay valores.</summary>
        public static decimal? Mediana(IEnumerable<decimal> valores)
        {
            var orden = valores.OrderBy(v => v).ToList();
            if (orden.Count == 0) return null;
            var medio = orden.Count / 2;
            return orden.Count % 2 == 1 ? orden[medio] : (orden[medio - 1] + orden[medio]) / 2m;
        }
    }

    /// <summary>Resultado de R-RIR-1 cuando se pudo evaluar.</summary>
    public sealed record EvaluacionRir(int RirObjetivo, decimal MedianaRealizada, int SeriesConDato, decimal Diferencia, bool Alerta);

    /// <summary>
    /// R-RIR-1, esfuerzo mayor al planificado: sobre las series de trabajo (sin calentamiento) con RIR registrado se toma
    /// la mediana del RIR realizado. Alerta si RIR objetivo menos esa mediana es mayor o igual a la diferencia configurada.
    /// No evalúa (null) sin RIR objetivo o con menos series con dato que el mínimo.
    /// </summary>
    public static class ReglaRir
    {
        public const string Codigo = "R-RIR-1";

        public static EvaluacionRir? Evaluar(IEnumerable<SerieEvaluada> series, int? rirObjetivo, ReglasSeguridadConfig.ConfigRir c)
        {
            if (rirObjetivo is not int objetivo) return null;
            var conDato = series.Where(s => !s.EsCalentamiento && s.Rir is not null).Select(s => (decimal)s.Rir!.Value).ToList();
            if (conDato.Count < c.SeriesMinimasConDato) return null;

            var mediana = Estadistica.Mediana(conDato)!.Value;
            var diferencia = objetivo - mediana;
            return new EvaluacionRir(objetivo, mediana, conDato.Count, diferencia, diferencia >= c.DiferenciaMinima);
        }
    }

    public enum DireccionCarga
    {
        Subir = 1,
        Bajar = 2
    }

    /// <summary>Sugerencia de R-PROG-1. Es informativa: nunca modifica la prescripción (decisión D5).</summary>
    public sealed record SugerenciaCarga(DireccionCarga Direccion, decimal CargaActualKg, decimal CargaSugeridaKg, decimal CambioKg,
        decimal CambioPorcentaje);

    /// <summary>
    /// R-PROG-1, doble progresión simple. Solo para ejercicios medidos en repeticiones, con carga mayor que 0 y sin
    /// alerta R-RIR-1 en el mismo ejercicio. Condiciones acordadas con el autor (P1 a P3):
    ///   - todas las series de trabajo deben tener el mismo peso (si no, no hay "una" carga y no se sugiere);
    ///   - con datos faltantes (alguna serie de trabajo sin RIR, o sin RIR objetivo) no se sugiere;
    ///   - Subir: al menos SeriesObjetivo series de trabajo, todas en RepeticionesMaximas o más, y mediana del RIR mayor
    ///     o igual al objetivo. Carga + IncrementoPorcentaje, redondeada; cambio mínimo CambioMinimoKg;
    ///   - Bajar: al menos la mitad de las series de trabajo por debajo de RepeticionesMinimas. Carga − DecrementoPorcentaje,
    ///     redondeada; cambio mínimo CambioMinimoKg;
    ///   - el tope (TopePorcentaje) gana sobre el cambio mínimo: si el cambio lo supera, no se sugiere. En la práctica,
    ///     con los valores por defecto no hay sugerencias por debajo de 5 kg;
    ///   - cualquier otro caso es "mantener" y devuelve null (no se registra nada).
    /// </summary>
    public static class ReglaProgresion
    {
        public const string Codigo = "R-PROG-1";

        public static SugerenciaCarga? Evaluar(IEnumerable<SerieEvaluada> series, ObjetivoEjercicio objetivo, bool hayAlertaRir,
            ReglasSeguridadConfig.ConfigProgresion c)
        {
            if (objetivo.TipoMedida != TipoMedida.Repeticiones || hayAlertaRir) return null;
            if (objetivo.RirObjetivo is not int rirObjetivo) return null;

            var trabajo = series.Where(s => !s.EsCalentamiento).ToList();
            if (trabajo.Count == 0) return null;
            if (trabajo.Any(s => s.Rir is null || s.Repeticiones <= 0)) return null;   // datos faltantes
            var carga = trabajo[0].PesoKg;
            if (carga <= 0 || trabajo.Any(s => s.PesoKg != carga)) return null;        // peso corporal o cargas distintas

            var mediana = Estadistica.Mediana(trabajo.Select(s => (decimal)s.Rir!.Value))!.Value;
            var subir = trabajo.Count >= objetivo.SeriesObjetivo
                        && trabajo.All(s => s.Repeticiones >= objetivo.RepeticionesMaximas)
                        && mediana >= rirObjetivo;
            if (subir) return Calcular(DireccionCarga.Subir, carga, c);

            var debajoDelMinimo = trabajo.Count(s => s.Repeticiones < objetivo.RepeticionesMinimas);
            if (debajoDelMinimo * 2 >= trabajo.Count) return Calcular(DireccionCarga.Bajar, carga, c);

            return null;
        }

        /// <summary>Redondea al múltiplo de <paramref name="paso"/> más cercano; los puntos medios, hacia arriba.</summary>
        public static decimal Redondear(decimal valor, decimal paso) =>
            Math.Round(valor / paso, MidpointRounding.AwayFromZero) * paso;

        private static SugerenciaCarga? Calcular(DireccionCarga direccion, decimal carga, ReglasSeguridadConfig.ConfigProgresion c)
        {
            var signo = direccion == DireccionCarga.Subir ? 1m : -1m;
            var porcentaje = direccion == DireccionCarga.Subir ? c.IncrementoPorcentaje : c.DecrementoPorcentaje;

            var sugerida = Redondear(carga * (1m + signo * porcentaje / 100m), c.RedondeoKg);
            var cambio = Math.Abs(sugerida - carga);
            if (cambio < c.CambioMinimoKg)
            {
                // Cambio mínimo, manteniendo la carga en múltiplos del redondeo (hacia afuera, para no quedar debajo del mínimo).
                var objetivo = carga + signo * c.CambioMinimoKg;
                sugerida = (signo > 0 ? Math.Ceiling(objetivo / c.RedondeoKg) : Math.Floor(objetivo / c.RedondeoKg)) * c.RedondeoKg;
                cambio = Math.Abs(sugerida - carga);
            }

            // El tope gana sobre el cambio mínimo (decisión P2): si se supera, no se sugiere.
            if (cambio > carga * c.TopePorcentaje / 100m || sugerida <= 0) return null;

            return new SugerenciaCarga(direccion, carga, sugerida, signo * cambio, Math.Round(signo * cambio * 100m / carga, 1));
        }
    }

    /// <summary>Resultado de R-FC-1 cuando se pudo evaluar.</summary>
    public sealed record EvaluacionFcReposo(short ValorDia, decimal LineaBase, int RegistrosLineaBase, decimal DiferenciaLpm,
        decimal VariacionPorcentaje, bool Alerta);

    /// <summary>
    /// R-FC-1, frecuencia cardíaca en reposo elevada respecto de la propia línea base. Línea base = mediana de la FC de
    /// reposo de los días anteriores (los días sin dato no cuentan); hacen falta al menos RegistrosMinimos. Alerta si el
    /// valor del día la supera en UmbralPorcentaje Y en UmbralLpm (las dos condiciones). No usa FC de reserva ni FC máxima.
    /// </summary>
    public static class ReglaFcReposo
    {
        public const string Codigo = "R-FC-1";

        public static EvaluacionFcReposo? Evaluar(short? valorDia, IEnumerable<short?> diasAnteriores, ReglasSeguridadConfig.ConfigFcReposo c)
        {
            if (valorDia is not short valor) return null;
            var conDato = diasAnteriores.Where(v => v is not null).Select(v => (decimal)v!.Value).ToList();
            if (conDato.Count < c.RegistrosMinimos) return null;

            var lineaBase = Estadistica.Mediana(conDato)!.Value;
            var diferencia = valor - lineaBase;
            var variacion = diferencia * 100m / lineaBase;
            var alerta = variacion >= c.UmbralPorcentaje && diferencia >= c.UmbralLpm;
            return new EvaluacionFcReposo(valor, lineaBase, conDato.Count, diferencia, Math.Round(variacion, 1), alerta);
        }
    }
}
