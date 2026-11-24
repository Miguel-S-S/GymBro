using GymBro.Domain.Enums;
using GymBro.Domain.Exceptions;

namespace GymBro.Domain.Rules
{
    // Etapa 7d: generación determinista de la rutina de extremidad inferior. Función pura: sin acceso a datos, sin reloj
    // y sin azar. Mismas entradas, mismas salidas (incluido el orden). No usa IA, embeddings ni pgvector.
    //
    // La dosificación y las exclusiones por limitación articular son criterios de diseño provisionales (decisión D5),
    // sin fuente bibliográfica ni validación clínica; los revisa la Personal Trainer antes de usarse con personas reales.

    /// <summary>
    /// Último registro de un ejercicio en el historial del socio: series de esa sesión y la línea de prescripción que se
    /// usó (null si la sesión no tenía prescripción o no se pudo relacionar).
    /// </summary>
    public sealed record HistorialEjercicio(Guid EjercicioId, Guid SesionId, DateTime Inicio, IReadOnlyList<SerieEvaluada> Series,
        ObjetivoEjercicio? LineaUsada)
    {
        /// <summary>% de series de trabajo dentro del rango de la línea usada. Null sin línea o sin series de trabajo.</summary>
        public decimal? AdherenciaPorcentaje =>
            LineaUsada is null ? null : Adherencia.AlRango(Series, LineaUsada.RepeticionesMinimas, LineaUsada.RepeticionesMaximas);

        /// <summary>Carga máxima de las series de trabajo (null si no hay series de trabajo con carga).</summary>
        public decimal? CargaMaximaKg
        {
            get
            {
                var cargas = Series.Where(s => !s.EsCalentamiento && s.PesoKg > 0).Select(s => s.PesoKg).ToList();
                return cargas.Count == 0 ? null : cargas.Max();
            }
        }
    }

    public static class Adherencia
    {
        /// <summary>Porcentaje de series de trabajo (sin calentamiento) con minimas &lt;= repeticiones &lt;= maximas. Null si no hay.</summary>
        public static decimal? AlRango(IEnumerable<SerieEvaluada> series, int minimas, int maximas)
        {
            var trabajo = series.Where(s => !s.EsCalentamiento).ToList();
            if (trabajo.Count == 0) return null;
            return trabajo.Count(s => s.Repeticiones >= minimas && s.Repeticiones <= maximas) * 100m / trabajo.Count;
        }
    }

    /// <summary>Perfil del socio para la planificación. <see cref="Historial"/> se indexa por ejercicio.</summary>
    public sealed record PerfilPlanificacion(Guid SocioId, int Edad, NivelExperiencia Nivel, NivelLimitacion LimitacionRodilla,
        NivelLimitacion LimitacionLumbar, int NumeroGeneracion, bool AlertaVigente, IReadOnlyDictionary<Guid, HistorialEjercicio> Historial);

    /// <summary>Fila del protocolo con los datos del ejercicio y si es factible con el equipamiento del gimnasio del socio.</summary>
    public sealed record CandidatoProtocolo(Guid EjercicioId, string Nombre, GrupoMuscularPiernas Grupo, RolEjercicioProtocolo Rol,
        ClaseEjercicio Clase, NivelLimitacion? RodillaExcluirDesde, NivelLimitacion? LumbarExcluirDesde, short OrdenRotacion,
        bool Factible, TipoMedida TipoMedida = TipoMedida.Repeticiones);

    /// <summary>Candidato excluido por una limitación articular (Articulacion: "rodilla" o "lumbar").</summary>
    public sealed record ExclusionLimitacion(Guid EjercicioId, string Articulacion, string NivelSocio, string ExcluirDesde);

    /// <summary>
    /// Motivo de la selección de una línea (se guarda como JSON en prescripciones_ejercicios.motivo_seleccion): posición,
    /// candidatos elegibles en el orden de rotación, índice, exclusiones y reglas aplicadas.
    /// </summary>
    public sealed record MotivoSeleccion(
        string ProtocoloVersion,
        int Posicion,
        string Grupo,
        string Rol,
        IReadOnlyList<Guid> Candidatos,
        int CantidadCandidatos,
        int? IndiceRotacion,
        IReadOnlyList<Guid> ExcluidosPorEquipamiento,
        IReadOnlyList<ExclusionLimitacion> ExcluidosPorLimitacion,
        IReadOnlyList<string> ReglasAplicadas,
        bool ReduccionPorAlerta,
        Guid? SesionHistorial,
        decimal? AdherenciaUltimoRegistro,
        string FuenteCargaSugerida);

    public sealed record LineaPlanificada(int Orden, Guid EjercicioId, string Nombre, RolEjercicioProtocolo Rol,
        GrupoMuscularPiernas Grupo, ClaseEjercicio Clase, TipoMedida TipoMedida, int Series, int RepeticionesMinimas,
        int RepeticionesMaximas, int Rir, int DescansoSegundos, short TempoExcentricoSeg, decimal? CargaSugeridaKg,
        MotivoSeleccion Motivo);

    /// <summary>Posición de la composición que quedó sin ejercicio (no se sustituye por otro fuera de la regla).</summary>
    public sealed record PosicionOmitida(int Posicion, string Grupo, string Rol, string Motivo,
        IReadOnlyList<Guid> ExcluidosPorEquipamiento, IReadOnlyList<ExclusionLimitacion> ExcluidosPorLimitacion);

    public sealed record ResultadoPlanificacion(IReadOnlyList<LineaPlanificada> Lineas, IReadOnlyList<PosicionOmitida> Omitidas,
        bool ReduccionPorAlerta, int SeriesTotales);

    /// <summary>
    /// Planificador del protocolo de piernas (Etapa 7d, especificación 5.3). Por cada posición de la composición:
    /// 1) elegibles = candidatos del grupo y rol, factibles por equipamiento y no excluidos por limitación (limitación del
    ///    socio mayor o igual al umbral de la fila); 2) referencia: el ejercicio fijo si es elegible, si no la posición se
    ///    omite; 3) variable: elegibles ordenados por orden_rotacion y id, índice (numeroGeneracion - 1) mod n;
    /// 4) series base por clase (pantorrilla aparte) y +1 con buena adherencia, sin alerta y nivel distinto de
    ///    Principiante, hasta el máximo por ejercicio y respetando el total por sesión en el orden de la composición;
    /// 5) con alerta vigente, series = max(piso, round(series x (1 - reducción))), sin superar las series previas;
    /// 6) repeticiones, descanso y tempo según la configuración; 7) RIR 2, o 1 con historial y buena adherencia, sin
    ///    alerta y nivel distinto de Principiante; nunca 0; 8) carga sugerida informativa (R-PROG-1, carga actual o null);
    /// 9) validación de salida: al menos un ejercicio por grupo, sin RIR 0, sin repetidos y total dentro del máximo.
    /// </summary>
    public static class PlanificadorPiernas
    {
        public const string ReglaSeriesBase = "SERIES_BASE";
        public const string ReglaSerieExtra = "SERIE_EXTRA_ADHERENCIA";
        public const string ReglaTopeSesion = "TOPE_SERIES_SESION";
        public const string ReglaReduccionAlerta = "REDUCCION_ALERTA";
        public const string ReglaAjusteEdadLimitacion = "AJUSTE_EDAD_O_LIMITACION";
        public const string ReglaRirReducido = "RIR_REDUCIDO_ADHERENCIA";

        public const string FuenteProgresion = "R-PROG-1";
        public const string FuenteCargaActual = "CARGA_ACTUAL";
        public const string FuenteSinHistorial = "SIN_HISTORIAL";
        public const string FuenteSinCargaUniforme = "SIN_CARGA_UNIFORME";
        public const string FuenteSegundos = "NO_APLICA_SEGUNDOS";

        public static string NombreGrupo(GrupoMuscularPiernas g) => g switch
        {
            GrupoMuscularPiernas.Cuadriceps => "Cuádriceps",
            GrupoMuscularPiernas.Isquiotibiales => "Isquiotibiales",
            GrupoMuscularPiernas.Pantorrillas => "Pantorrillas",
            _ => g.ToString()
        };

        private sealed class Borrador
        {
            public required CandidatoProtocolo Candidato { get; init; }
            public required int Posicion { get; init; }
            public required PosicionProtocolo Definicion { get; init; }
            public required List<Guid> Elegibles { get; init; }
            public int? Indice { get; init; }
            public required List<Guid> PorEquipamiento { get; init; }
            public required List<ExclusionLimitacion> PorLimitacion { get; init; }
            public HistorialEjercicio? Historial { get; init; }
            public int Series { get; set; }
            public List<string> Reglas { get; } = new();
        }

        public static ResultadoPlanificacion Planificar(PerfilPlanificacion perfil, IReadOnlyCollection<CandidatoProtocolo> candidatos,
            ProtocoloPiernasConfig c, ReglasSeguridadConfig reglas)
        {
            if (perfil.NumeroGeneracion < 1)
                throw new ArgumentOutOfRangeException(nameof(perfil), "El número de generación empieza en 1.");

            var composicion = c.ComposicionEfectiva();
            var borradores = new List<Borrador>();
            var omitidas = new List<PosicionOmitida>();

            // 1 a 3) Selección por posición.
            for (var i = 0; i < composicion.Count; i++)
            {
                var pos = composicion[i];
                var numero = i + 1;
                var delaPosicion = candidatos
                    .Where(x => x.Grupo == pos.Grupo && x.Rol == pos.Rol)
                    .Where(x => pos.Rol != RolEjercicioProtocolo.Referencia || x.OrdenRotacion == pos.OrdenReferencia)
                    .OrderBy(x => x.OrdenRotacion).ThenBy(x => x.EjercicioId)
                    .ToList();

                var porEquipamiento = delaPosicion.Where(x => !x.Factible).Select(x => x.EjercicioId).ToList();
                var porLimitacion = delaPosicion.SelectMany(x => Exclusiones(x, perfil)).ToList();
                var excluidos = porEquipamiento.Concat(porLimitacion.Select(x => x.EjercicioId)).ToHashSet();
                var elegibles = delaPosicion
                    .Where(x => !excluidos.Contains(x.EjercicioId))
                    .Where(x => borradores.All(b => b.Candidato.EjercicioId != x.EjercicioId))   // sin repetidos
                    .ToList();

                if (elegibles.Count == 0)
                {
                    var motivo = delaPosicion.Count == 0
                        ? "El catálogo del protocolo no tiene un ejercicio para esta posición."
                        : pos.Rol == RolEjercicioProtocolo.Referencia
                            ? "La referencia no es elegible (equipamiento o limitación articular); se omite y no se sustituye."
                            : "Ningún candidato elegible (equipamiento o limitación articular).";
                    omitidas.Add(new PosicionOmitida(numero, pos.Grupo.ToString(), pos.Rol.ToString(), motivo, porEquipamiento, porLimitacion));
                    continue;
                }

                int? indice = pos.Rol == RolEjercicioProtocolo.Variable ? (perfil.NumeroGeneracion - 1) % elegibles.Count : null;
                var elegido = elegibles[indice ?? 0];
                perfil.Historial.TryGetValue(elegido.EjercicioId, out var historial);

                borradores.Add(new Borrador
                {
                    Candidato = elegido,
                    Posicion = numero,
                    Definicion = pos,
                    Elegibles = elegibles.Select(x => x.EjercicioId).ToList(),
                    Indice = indice,
                    PorEquipamiento = porEquipamiento,
                    PorLimitacion = porLimitacion,
                    Historial = historial,
                });
            }

            // 9a) Al menos un ejercicio por grupo de la composición (antes de dosificar: si falta, no hay rutina).
            foreach (var grupo in composicion.Select(p => p.Grupo).Distinct())
            {
                if (borradores.Any(b => b.Candidato.Grupo == grupo)) continue;
                var nombre = NombreGrupo(grupo);
                if (!candidatos.Any(x => x.Grupo == grupo))
                    throw new ReglaNegocioException(
                        $"No se puede generar la rutina de piernas: el catálogo del protocolo no tiene ejercicios del grupo {nombre}.");
                throw new ReglaNegocioException(
                    $"No se puede generar la rutina de piernas: no hay ejercicios elegibles del grupo {nombre} " +
                    "con el equipamiento del gimnasio y las limitaciones articulares registradas.");
            }

            // 4) Series base y serie extra por adherencia, respetando el total por sesión en el orden de la composición.
            var ajuste = perfil.Edad >= c.EdadAjuste
                         || perfil.LimitacionRodilla >= NivelLimitacion.Moderada
                         || perfil.LimitacionLumbar >= NivelLimitacion.Moderada;
            foreach (var b in borradores)
            {
                b.Series = SeriesBase(b.Candidato, c);
                b.Reglas.Add(ReglaSeriesBase);
            }

            var total = borradores.Sum(b => b.Series);
            foreach (var b in borradores)
            {
                if (!BuenaAdherencia(b.Historial, perfil, c)) continue;
                if (b.Series >= SeriesMaximas(b.Candidato, c)) continue;
                if (total + 1 > c.SeriesTotalesMaxSesion) { b.Reglas.Add(ReglaTopeSesion); continue; }
                b.Series++;
                total++;
                b.Reglas.Add(ReglaSerieExtra);
            }

            // 5) Reducción por alerta vigente.
            if (perfil.AlertaVigente)
            {
                var factor = 1m - c.ReduccionPorAlertaPorcentaje / 100m;
                foreach (var b in borradores)
                {
                    var reducidas = (int)Math.Round(b.Series * factor, MidpointRounding.AwayFromZero);
                    b.Series = Math.Min(b.Series, Math.Max(c.SeriesMinimasConAlerta, reducidas));
                    b.Reglas.Add(ReglaReduccionAlerta);
                }
            }

            // 6 a 8) Repeticiones, descanso, tempo, RIR y carga sugerida.
            var rirMinimo = Math.Max(1, c.RirMinimo);
            var lineas = new List<LineaPlanificada>();
            var orden = 0;
            foreach (var b in borradores)
            {
                var x = b.Candidato;
                var rango = x.Grupo == GrupoMuscularPiernas.Pantorrillas ? c.RepeticionesPantorrilla
                    : x.Clase == ClaseEjercicio.Multiarticular ? c.RepeticionesMultiarticular
                    : c.RepeticionesAislamiento;
                var descanso = x.Clase == ClaseEjercicio.Multiarticular
                    ? (ajuste ? c.DescansoMultiarticularAjustadoSeg : c.DescansoMultiarticularSeg)
                    : c.DescansoAislamientoSeg;
                var tempo = (short)(ajuste ? c.TempoExcentricoAjustadoSeg : c.TempoExcentricoSeg);
                if (ajuste) b.Reglas.Add(ReglaAjusteEdadLimitacion);

                var rir = Math.Max(rirMinimo, c.RirObjetivo);
                if (BuenaAdherencia(b.Historial, perfil, c))
                {
                    rir = Math.Max(rirMinimo, c.RirObjetivo - 1);
                    b.Reglas.Add(ReglaRirReducido);
                }

                var (carga, fuente) = CargaSugerida(x, b.Historial, reglas);

                orden++;
                lineas.Add(new LineaPlanificada(orden, x.EjercicioId, x.Nombre, x.Rol, x.Grupo, x.Clase, x.TipoMedida, b.Series,
                    rango.Minimas, rango.Maximas, rir, descanso, tempo, carga,
                    new MotivoSeleccion(c.Version, b.Posicion, x.Grupo.ToString(), x.Rol.ToString(), b.Elegibles, b.Elegibles.Count,
                        b.Indice, b.PorEquipamiento, b.PorLimitacion, b.Reglas.ToList(), perfil.AlertaVigente,
                        b.Historial?.SesionId, b.Historial?.AdherenciaPorcentaje, fuente)));
            }

            // 9b) Validación de salida.
            if (lineas.Any(l => l.Rir < 1))
                throw new InvalidOperationException("El planificador produjo un RIR menor que 1.");
            if (lineas.GroupBy(l => l.EjercicioId).Any(g => g.Count() > 1))
                throw new InvalidOperationException("El planificador produjo un ejercicio repetido.");
            var seriesTotales = lineas.Sum(l => l.Series);
            if (seriesTotales > c.SeriesTotalesMaxSesion)
                throw new ReglaNegocioException(
                    $"La configuración del protocolo produce {seriesTotales} series y el máximo por sesión es {c.SeriesTotalesMaxSesion}.");

            return new ResultadoPlanificacion(lineas, omitidas, perfil.AlertaVigente, seriesTotales);
        }

        private static IEnumerable<ExclusionLimitacion> Exclusiones(CandidatoProtocolo x, PerfilPlanificacion p)
        {
            if (x.RodillaExcluirDesde is NivelLimitacion r && p.LimitacionRodilla >= r)
                yield return new ExclusionLimitacion(x.EjercicioId, "rodilla", p.LimitacionRodilla.ToString(), r.ToString());
            if (x.LumbarExcluirDesde is NivelLimitacion l && p.LimitacionLumbar >= l)
                yield return new ExclusionLimitacion(x.EjercicioId, "lumbar", p.LimitacionLumbar.ToString(), l.ToString());
        }

        private static int SeriesBase(CandidatoProtocolo x, ProtocoloPiernasConfig c) =>
            x.Grupo == GrupoMuscularPiernas.Pantorrillas ? c.SeriesBasePantorrilla
            : x.Clase == ClaseEjercicio.Multiarticular ? c.SeriesBaseMultiarticular
            : c.SeriesBaseAislamiento;

        private static int SeriesMaximas(CandidatoProtocolo x, ProtocoloPiernasConfig c) =>
            x.Clase == ClaseEjercicio.Multiarticular ? c.SeriesMaxMultiarticular : c.SeriesMaxAislamiento;

        /// <summary>Historial del ejercicio con adherencia suficiente, sin alerta vigente y nivel distinto de Principiante.</summary>
        private static bool BuenaAdherencia(HistorialEjercicio? h, PerfilPlanificacion p, ProtocoloPiernasConfig c) =>
            h?.AdherenciaPorcentaje is decimal a && a >= c.AdherenciaMinimaPorcentaje
            && !p.AlertaVigente && p.Nivel != NivelExperiencia.Principiante;

        /// <summary>
        /// Carga sugerida (informativa): con historial, R-PROG-1 sobre el último registro y la línea usada; si no sugiere y
        /// las series de trabajo tienen una misma carga mayor que 0, esa carga; sin historial, null. Nunca para segundos.
        /// </summary>
        private static (decimal? Carga, string Fuente) CargaSugerida(CandidatoProtocolo x, HistorialEjercicio? h, ReglasSeguridadConfig reglas)
        {
            if (x.TipoMedida == TipoMedida.Segundos) return (null, FuenteSegundos);
            if (h is null) return (null, FuenteSinHistorial);

            if (h.LineaUsada is ObjetivoEjercicio objetivo)
            {
                var obj = objetivo with { TipoMedida = x.TipoMedida };
                var rir = ReglaRir.Evaluar(h.Series, obj.RirObjetivo, reglas.Rir);
                var sugerencia = ReglaProgresion.Evaluar(h.Series, obj, rir is { Alerta: true }, reglas.Progresion);
                if (sugerencia is not null) return (sugerencia.CargaSugeridaKg, FuenteProgresion);
            }

            var trabajo = h.Series.Where(s => !s.EsCalentamiento).ToList();
            if (trabajo.Count > 0 && trabajo[0].PesoKg > 0 && trabajo.All(s => s.PesoKg == trabajo[0].PesoKg))
                return (trabajo[0].PesoKg, FuenteCargaActual);
            return (null, FuenteSinCargaUniforme);
        }
    }
}
