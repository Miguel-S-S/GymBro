using System;
using GymBro.Domain.Enums;

namespace GymBro.Domain.Entities
{
    public class PrescripcionEjercicio : EntidadAuditable
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid PrescripcionEntrenamientoId { get; set; }
        public PrescripcionEntrenamiento PrescripcionEntrenamiento { get; set; } = null!;

        public Guid EjercicioId { get; set; }
        public Ejercicio Ejercicio { get; set; } = null!;

        public int Orden { get; set; }
        public int SeriesObjetivo { get; set; }
        /// <summary>
        /// Repeticiones mínimas. Si el ejercicio se mide en segundos (<c>Ejercicio.TipoMedida</c>), guarda segundos:
        /// decisión de diseño de la Etapa 9a-1 para no renombrar columnas; el cliente muestra la unidad según el tipo.
        /// </summary>
        public int RepeticionesMinimas { get; set; }
        /// <summary>Repeticiones máximas; segundos si el ejercicio se mide en segundos (ver <see cref="RepeticionesMinimas"/>).</summary>
        public int RepeticionesMaximas { get; set; }
        public int? RIRObjetivo { get; set; }
        public int DescansoObjetivoSegundos { get; set; }

        public string? Observaciones { get; set; }

        // ---- Etapa 7d: trazabilidad de la línea generada por un protocolo (null en las demás rutinas) ----

        public RolEjercicioProtocolo? Rol { get; set; }
        public GrupoMuscularPiernas? Grupo { get; set; }

        /// <summary>Duración de la fase excéntrica en segundos (valor provisional, sin validación clínica).</summary>
        public short? TempoExcentricoSeg { get; set; }

        /// <summary>Carga sugerida a partir del historial. Solo informativa: no obliga ni se valida.</summary>
        public decimal? CargaSugeridaKg { get; set; }

        /// <summary>JSON (jsonb) con el motivo de la selección: posición, candidatos, rotación, exclusiones y reglas.</summary>
        public string? MotivoSeleccion { get; set; }
    }
}
