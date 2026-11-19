using GymBro.Domain.Enums;

namespace GymBro.Domain.Rules
{
    /// <summary>Línea de una prescripción, con lo que necesita el validador (independiente de la entidad).</summary>
    public sealed record LineaPrescripcion(Guid EjercicioId, int Orden, string? Ejercicio, int Series, int RepeticionesMinimas,
        int RepeticionesMaximas, TipoMedida TipoMedida, int? RirObjetivo, int DescansoSegundos);

    /// <summary>Violación de un límite duro. <see cref="Orden"/> es null si la violación es de la rutina completa.</summary>
    public sealed record ViolacionLimite(string Codigo, string Mensaje, int? Orden);

    /// <summary>
    /// R-LIM (Etapa 10a): límites duros de una prescripción. Función pura: no bloquea nada por sí misma; quien la usa
    /// decide (el baseline rechaza la generación; la revisión advierte y registra). Los límites son decisiones de diseño
    /// provisionales, sin fuente bibliográfica. Un RIR objetivo nulo ("sin objetivo") no se evalúa.
    /// </summary>
    public static class ValidadorPrescripcion
    {
        public const string Series = "LIM-SERIES";
        public const string Repeticiones = "LIM-REPETICIONES";
        public const string Segundos = "LIM-SEGUNDOS";
        public const string Descanso = "LIM-DESCANSO";
        public const string Rir = "LIM-RIR";
        public const string RirPrincipiante = "LIM-RIR-PRINCIPIANTE";
        public const string CantidadEjercicios = "LIM-CANTIDAD-EJERCICIOS";
        public const string SeriesTotales = "LIM-SERIES-TOTALES";
        public const string EjercicioRepetido = "LIM-EJERCICIO-REPETIDO";

        public static IReadOnlyList<ViolacionLimite> Validar(IReadOnlyCollection<LineaPrescripcion> lineas, NivelExperiencia? nivel,
            LimitesPrescripcion l)
        {
            var v = new List<ViolacionLimite>();

            if (lineas.Count < l.EjerciciosMin || lineas.Count > l.EjerciciosMax)
                v.Add(new(CantidadEjercicios, $"La rutina tiene {lineas.Count} ejercicios; el rango admitido es {l.EjerciciosMin} a {l.EjerciciosMax}.", null));

            var totales = lineas.Sum(x => x.Series);
            if (totales > l.SeriesTotalesMax)
                v.Add(new(SeriesTotales, $"La rutina suma {totales} series; el máximo es {l.SeriesTotalesMax}.", null));

            foreach (var x in lineas.OrderBy(x => x.Orden))
            {
                var nombre = string.IsNullOrWhiteSpace(x.Ejercicio) ? $"Ejercicio {x.Orden}" : x.Ejercicio;

                if (x.Series < l.SeriesMin || x.Series > l.SeriesMax)
                    v.Add(new(Series, $"{nombre}: {x.Series} series; el rango admitido es {l.SeriesMin} a {l.SeriesMax}.", x.Orden));

                if (x.TipoMedida == TipoMedida.Segundos)
                {
                    if (x.RepeticionesMinimas < l.SegundosMin || x.RepeticionesMaximas > l.SegundosMax)
                        v.Add(new(Segundos, $"{nombre}: {x.RepeticionesMinimas}-{x.RepeticionesMaximas} s; el rango admitido es {l.SegundosMin} a {l.SegundosMax} s.", x.Orden));
                }
                else if (x.RepeticionesMinimas < l.RepeticionesMin || x.RepeticionesMaximas > l.RepeticionesMax)
                {
                    v.Add(new(Repeticiones, $"{nombre}: {x.RepeticionesMinimas}-{x.RepeticionesMaximas} repeticiones; el rango admitido es {l.RepeticionesMin} a {l.RepeticionesMax}.", x.Orden));
                }

                if (x.DescansoSegundos < l.DescansoMinSegundos || x.DescansoSegundos > l.DescansoMaxSegundos)
                    v.Add(new(Descanso, $"{nombre}: descanso de {x.DescansoSegundos} s; el rango admitido es {l.DescansoMinSegundos} a {l.DescansoMaxSegundos} s.", x.Orden));

                if (x.RirObjetivo is int rir)
                {
                    if (rir < l.RirMin || rir > l.RirMax)
                        v.Add(new(Rir, $"{nombre}: RIR objetivo {rir}; el rango admitido es {l.RirMin} a {l.RirMax}.", x.Orden));
                    else if (nivel == NivelExperiencia.Principiante && rir < l.RirMinPrincipiante)
                        v.Add(new(RirPrincipiante, $"{nombre}: RIR objetivo {rir}; para un socio principiante el mínimo es {l.RirMinPrincipiante}.", x.Orden));
                }
            }

            foreach (var repetido in lineas.GroupBy(x => x.EjercicioId).Where(g => g.Count() > 1))
            {
                var nombre = repetido.First().Ejercicio ?? "Un ejercicio";
                v.Add(new(EjercicioRepetido, $"{nombre} aparece {repetido.Count()} veces en la rutina.", null));
            }

            return v;
        }
    }
}
