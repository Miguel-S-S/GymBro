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
        public string Mensaje { get; set; } = string.Empty;
    }
}
