using System;
using System.Collections.Generic;

namespace GymBro.Application.DTOs.Comparacion
{
    public class ComparacionSesionResponseDto
    {
        public Guid SesionId { get; set; }
        public Guid? PrescripcionEntrenamientoId { get; set; }
        public List<EjercicioComparadoDto> Ejercicios { get; set; } = new List<EjercicioComparadoDto>();
    }

    public class EjercicioComparadoDto
    {
        public Guid EjercicioId { get; set; }
        public int Orden { get; set; }
        /// <summary>
        /// "Repeticiones" o "Segundos", tomado del ejercicio (no de la prescripción; Etapa 9a-1). Con Segundos,
        /// los campos de repeticiones guardan segundos y los volúmenes en kg valen 0.
        /// </summary>
        public string TipoMedida { get; set; } = "Repeticiones";

        /// <summary>Etapa 7d (aditivo): línea de la prescripción con la que se comparó; null si no hay.</summary>
        public Guid? PrescripcionEjercicioId { get; set; }

        public PrescripcionResumenDto? Prescripcion { get; set; }
        public EjecucionResumenDto? Ejecucion { get; set; }
        public ComparacionGeneralDto Comparacion { get; set; } = new ComparacionGeneralDto();
    }

    public class PrescripcionResumenDto
    {
        public int SeriesObjetivo { get; set; }
        public int RepeticionesMinimas { get; set; }
        public int RepeticionesMaximas { get; set; }
        public int? RIRObjetivo { get; set; }
        public int DescansoObjetivoSegundos { get; set; }
    }

    public class EjecucionResumenDto
    {
        public int CantidadSeriesTotales { get; set; }
        public int CantidadSeriesEfectivas { get; set; }
        public int CantidadSeriesCalentamiento { get; set; }
        public decimal VolumenTotalKg { get; set; }
        public decimal VolumenEfectivoKg { get; set; }
        public decimal VolumenCalentamientoKg { get; set; }
        
        public List<SerieComparadaDto> DetallesSeries { get; set; } = new List<SerieComparadaDto>();
    }

    public class SerieComparadaDto
    {
        public int OrdenSerie { get; set; }
        public decimal PesoLevantadoKg { get; set; }
        public int Repeticiones { get; set; }
        public int? RIR { get; set; }
        public bool EsCalentamiento { get; set; }
        public int? TiempoDescansoSegundos { get; set; }
        public decimal Volumen { get; set; }

        public ComparacionMetricaDto? RepeticionesComparacion { get; set; }
        public ComparacionMetricaDto? RIRComparacion { get; set; }
    }

    public class ComparacionMetricaDto
    {
        public bool CumpleObjetivo { get; set; }
        public int Desviacion { get; set; }
        public string Mensaje { get; set; } = string.Empty;
    }

    public class ComparacionGeneralDto
    {
        public ComparacionSeriesDto Series { get; set; } = new ComparacionSeriesDto();
    }

    public class ComparacionSeriesDto
    {
        public int Prescritas { get; set; }
        public int RealizadasEfectivas { get; set; }
        public int Diferencia { get; set; }

        /// <summary>
        /// Obsoleto desde la Etapa 8d: veredicto "Cumple objetivo" / "Faltaron N series" / "Excedieron N series". La
        /// interfaz del Socio lo reemplazo por dos indicadores (series y repeticiones en rango) calculados con los demas
        /// campos. Etapa 8e: la vista del Dueno y del Personal Trainer tampoco lo muestra; ya no lo lee ninguna pantalla.
        /// Se conserva para no cambiar el contrato publico (la 8e es sin cambios de API); su retiro queda como propuesta.
        /// </summary>
        [Obsolete("Etapa 8d: usar Prescritas, RealizadasEfectivas y DetallesSeries; el veredicto unico ya no se muestra al Socio.")]
        public string Mensaje { get; set; } = string.Empty;
    }
}
