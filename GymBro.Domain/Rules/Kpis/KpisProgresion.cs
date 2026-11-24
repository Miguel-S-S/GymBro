using GymBro.Domain.Enums;

namespace GymBro.Domain.Rules.Kpis
{
    // Etapa 7d (D6): cuatro KPIs del estudio piloto como funciones puras. Son indicadores DESCRIPTIVOS: no predicen ni
    // establecen causalidad. Con datos sintéticos, K1 y K2 reflejan el modelo del generador de datos y no la fisiología.
    // Las alertas dadas de baja lógica cuando su condición deja de cumplirse no cuentan, lo que puede subestimar K4 en
    // períodos pasados.
    //
    // Criterio común: sesión efectiva = al menos una serie con EsCalentamiento = false; solo cuentan las series
    // efectivas; las filas con baja lógica no llegan acá (las descarta quien arma las entradas); el orden es por inicio de
    // la sesión y luego por id.

    public sealed record SerieKpi(decimal PesoKg, int Repeticiones, int? Rir, bool EsCalentamiento);

    /// <summary>Línea de prescripción (dosis planificada) con su id.</summary>
    public sealed record LineaKpi(Guid Id, Guid EjercicioId, int Series, int RepeticionesMinimas, int RepeticionesMaximas, int? RirObjetivo);

    /// <summary>
    /// Ejercicio ejecutado en una sesión. <see cref="Grupo"/> es el grupo del protocolo (null si el ejercicio no está en el
    /// protocolo). <see cref="Linea"/> es la línea de prescripción vinculada (vínculo directo o, en sesiones anteriores, por
    /// ejercicio y orden); null si no hay.
    /// </summary>
    public sealed record EjercicioSesionKpi(Guid EjercicioId, TipoMedida TipoMedida, GrupoMuscularPiernas? Grupo, LineaKpi? Linea,
        IReadOnlyList<SerieKpi> Series);

    /// <summary>
    /// Sesión con lo que necesitan los KPIs. <see cref="LineasPrescripcion"/> son todas las líneas de la rutina vinculada
    /// (vacía si no hay). <see cref="AlertaAsociada"/>: alerta vigente de tipo Alerta asociada a la sesión o, para R-FC-1,
    /// al bienestar del mismo día local. <see cref="SugerenciaBajarCarga"/>: sugerencia R-PROG-1 vigente de bajar carga en
    /// algún ejercicio de la sesión.
    /// </summary>
    public sealed record SesionKpi(Guid SesionId, DateTime Inicio, Guid? PrescripcionId, IReadOnlyList<EjercicioSesionKpi> Ejercicios,
        IReadOnlyList<LineaKpi> LineasPrescripcion, bool AlertaAsociada, bool SugerenciaBajarCarga)
    {
        public bool EsEfectiva => Ejercicios.Any(e => e.Series.Any(s => !s.EsCalentamiento));
    }

    // ---- Resultados ----

    public sealed record DeltaCarga(Guid EjercicioId, Guid SesionId, DateTime Inicio, decimal CargaMaximaKg, decimal? DeltaKg);

    public sealed record VolumenGrupo(GrupoMuscularPiernas Grupo, decimal VolumenKg, int SeriesEfectivas);

    public sealed record VolumenSesion(Guid SesionId, DateTime Inicio, IReadOnlyList<VolumenGrupo> PorGrupo, decimal VolumenTotalKg,
        int SeriesEfectivasTotales);

    /// <summary>
    /// K3 por ejercicio y sesión. Sin línea vinculada, los valores son null. DesvioRirMedio positivo = más reserva que la
    /// planificada. NRango y NRir son las series usadas en cada parte.
    /// </summary>
    public sealed record AdherenciaEjercicio(Guid SesionId, DateTime Inicio, Guid EjercicioId, Guid? PrescripcionEjercicioId,
        decimal? PorcentajeEnRango, int NRango, decimal? DesvioRirMedio, decimal? DesvioRirAbsolutoMedio, int NRir);

    public sealed record SesionSeguridad(Guid SesionId, DateTime Inicio, bool Limpia, bool AlertaAsociada, bool SugerenciaBajarCarga,
        bool? CumpleSeriesPrescritas);

    public sealed record IndiceSeguridad(int SesionesEfectivas, int SesionesLimpias, decimal? Indice, int GeneracionesConReduccionPorAlerta,
        IReadOnlyList<SesionSeguridad> Detalle);

    public static class KpisProgresion
    {
        private static IEnumerable<SesionKpi> Ordenadas(IEnumerable<SesionKpi> sesiones) =>
            sesiones.Where(s => s.EsEfectiva).OrderBy(s => s.Inicio).ThenBy(s => s.SesionId);

        private static IEnumerable<SerieKpi> Efectivas(EjercicioSesionKpi e) => e.Series.Where(s => !s.EsCalentamiento);

        /// <summary>
        /// K1, delta de carga máxima. Por ejercicio y sesión: carga máxima de las series efectivas con carga mayor que 0 y
        /// tipo Repeticiones (si el ejercicio aparece más de una vez en la sesión, el máximo de todas). Delta = carga de esta
        /// sesión menos la de la sesión anterior en la que el ejercicio tuvo carga máxima; null en la primera aparición.
        /// Una sesión en la que el ejercicio no tuvo series efectivas con carga no cuenta como aparición.
        /// </summary>
        public static IReadOnlyList<DeltaCarga> K1DeltaCargaMaxima(IEnumerable<SesionKpi> sesiones)
        {
            var resultado = new List<DeltaCarga>();
            var anterior = new Dictionary<Guid, decimal>();
            foreach (var s in Ordenadas(sesiones))
            {
                var maximos = s.Ejercicios
                    .Where(e => e.TipoMedida == TipoMedida.Repeticiones)
                    .SelectMany(e => Efectivas(e).Where(x => x.PesoKg > 0).Select(x => (e.EjercicioId, x.PesoKg)))
                    .GroupBy(x => x.EjercicioId)
                    .Select(g => (EjercicioId: g.Key, Maximo: g.Max(x => x.PesoKg)))
                    .OrderBy(x => x.EjercicioId);
                foreach (var (ejercicioId, maximo) in maximos)
                {
                    decimal? delta = anterior.TryGetValue(ejercicioId, out var previo) ? maximo - previo : null;
                    resultado.Add(new DeltaCarga(ejercicioId, s.SesionId, s.Inicio, maximo, delta));
                    anterior[ejercicioId] = maximo;
                }
            }
            return resultado;
        }

        /// <summary>
        /// K2, volumen efectivo. Por sesión y grupo del protocolo: suma de peso x repeticiones de las series efectivas, y la
        /// cantidad de series efectivas. Los ejercicios en segundos aportan 0 kg (como la comparación de sesiones) pero sí
        /// sus series. Los ejercicios fuera del protocolo suman solo al total de la sesión.
        /// </summary>
        public static IReadOnlyList<VolumenSesion> K2VolumenEfectivo(IEnumerable<SesionKpi> sesiones)
        {
            static decimal Volumen(EjercicioSesionKpi e) =>
                e.TipoMedida == TipoMedida.Segundos ? 0m : Efectivas(e).Sum(x => x.PesoKg * x.Repeticiones);

            return Ordenadas(sesiones).Select(s => new VolumenSesion(
                s.SesionId, s.Inicio,
                s.Ejercicios.Where(e => e.Grupo is not null)
                    .GroupBy(e => e.Grupo!.Value).OrderBy(g => g.Key)
                    .Select(g => new VolumenGrupo(g.Key, g.Sum(Volumen), g.Sum(e => Efectivas(e).Count())))
                    .ToList(),
                s.Ejercicios.Sum(Volumen),
                s.Ejercicios.Sum(e => Efectivas(e).Count()))).ToList();
        }

        /// <summary>
        /// K3, adherencia al rango y esfuerzo. Por ejercicio y sesión, con la línea vinculada: % de series efectivas con
        /// repeticiones dentro del rango; desvío RIR medio (rir - rir objetivo) y desvío absoluto medio, solo sobre series
        /// con RIR cargado y con RIR objetivo. Sin línea vinculada: valores null.
        /// </summary>
        public static IReadOnlyList<AdherenciaEjercicio> K3Adherencia(IEnumerable<SesionKpi> sesiones)
        {
            var resultado = new List<AdherenciaEjercicio>();
            foreach (var s in Ordenadas(sesiones))
            {
                foreach (var e in s.Ejercicios.Where(e => Efectivas(e).Any()))
                {
                    if (e.Linea is not LineaKpi l)
                    {
                        resultado.Add(new AdherenciaEjercicio(s.SesionId, s.Inicio, e.EjercicioId, null, null, 0, null, null, 0));
                        continue;
                    }
                    var efectivas = Efectivas(e).ToList();
                    var enRango = efectivas.Count(x => x.Repeticiones >= l.RepeticionesMinimas && x.Repeticiones <= l.RepeticionesMaximas);
                    var difs = l.RirObjetivo is int objetivo
                        ? efectivas.Where(x => x.Rir is not null).Select(x => (decimal)(x.Rir!.Value - objetivo)).ToList()
                        : new List<decimal>();
                    resultado.Add(new AdherenciaEjercicio(s.SesionId, s.Inicio, e.EjercicioId, l.Id,
                        enRango * 100m / efectivas.Count, efectivas.Count,
                        difs.Count == 0 ? null : difs.Average(), difs.Count == 0 ? null : difs.Average(Math.Abs), difs.Count));
                }
            }
            return resultado;
        }

        /// <summary>
        /// K4, índice de adaptación y seguridad = sesiones limpias / sesiones efectivas (null sin sesiones efectivas).
        /// Limpia = sin alerta asociada, sin sugerencia R-PROG-1 de bajar carga y, con prescripción vinculada, series
        /// efectivas realizadas mayores o iguales a las prescritas en todas sus líneas. Se informa además la cantidad de
        /// generaciones con reducción por alerta (dato de entrada).
        /// </summary>
        public static IndiceSeguridad K4IndiceSeguridad(IEnumerable<SesionKpi> sesiones, int generacionesConReduccionPorAlerta)
        {
            var detalle = new List<SesionSeguridad>();
            foreach (var s in Ordenadas(sesiones))
            {
                bool? cumple = null;
                if (s.PrescripcionId is not null && s.LineasPrescripcion.Count > 0)
                {
                    cumple = s.LineasPrescripcion.All(l =>
                        s.Ejercicios.Where(e => e.Linea?.Id == l.Id).Sum(e => Efectivas(e).Count()) >= l.Series);
                }
                var limpia = !s.AlertaAsociada && !s.SugerenciaBajarCarga && cumple != false;
                detalle.Add(new SesionSeguridad(s.SesionId, s.Inicio, limpia, s.AlertaAsociada, s.SugerenciaBajarCarga, cumple));
            }
            var limpias = detalle.Count(d => d.Limpia);
            return new IndiceSeguridad(detalle.Count, limpias, detalle.Count == 0 ? null : (decimal)limpias / detalle.Count,
                generacionesConReduccionPorAlerta, detalle);
        }
    }
}
