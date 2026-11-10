using System;
using System.Collections.Generic;

namespace GymBro.Application.DTOs
{
    public class SerieRealizadaResponseDto
    {
        public Guid Id { get; set; }
        public int OrdenSerie { get; set; }
        public decimal PesoLevantadoKg { get; set; }
        public int Repeticiones { get; set; }
        public int? RIR { get; set; }
        public bool EsCalentamiento { get; set; }
        public int? TiempoDescansoSegundos { get; set; }
    }

    public class SesionEjercicioResponseDto
    {
        public Guid Id { get; set; }
        public Guid EjercicioId { get; set; }
        public int Orden { get; set; }
        public string? Observaciones { get; set; }
        public List<SerieRealizadaResponseDto> SeriesRealizadas { get; set; } = new List<SerieRealizadaResponseDto>();
    }

    public class SesionEntrenamientoResponseDto
    {
        public Guid Id { get; set; }
        public Guid SocioId { get; set; }
        public Guid? PrescripcionEntrenamientoId { get; set; }
        public DateTime Inicio { get; set; }
        public DateTime? Fin { get; set; }
        public int CaloriasQuemadas { get; set; }
        public int FrecuenciaCardiacaMaxima { get; set; }
        public int FrecuenciaCardiacaPromedio { get; set; }
        public List<SesionEjercicioResponseDto> Ejercicios { get; set; } = new List<SesionEjercicioResponseDto>();
    }
}
