using System;
using System.Collections.Generic;

namespace GymBro.Application.DTOs
{
    public class SerieRealizadaRequestDto
    {
        public int OrdenSerie { get; set; }
        public decimal PesoLevantadoKg { get; set; }
        public int Repeticiones { get; set; }
        public int? RIR { get; set; }
        public bool EsCalentamiento { get; set; }
        public int? TiempoDescansoSegundos { get; set; }
    }

    public class SesionEjercicioRequestDto
    {
        public Guid EjercicioId { get; set; }
        public int Orden { get; set; }
        public string? Observaciones { get; set; }

        /// <summary>
        /// Etapa 7d (opcional): línea de la prescripción que se ejecuta. Si se indica, debe pertenecer a la prescripción de
        /// la sesión y ser del mismo ejercicio; si no, se resuelve por ejercicio (y orden si hay más de una línea).
        /// </summary>
        public Guid? PrescripcionEjercicioId { get; set; }
        public List<SerieRealizadaRequestDto> SeriesRealizadas { get; set; } = new List<SerieRealizadaRequestDto>();
    }

    public class RegistrarSesionEntrenamientoRequest
    {
        public Guid SocioId { get; set; }
        public Guid? PrescripcionEntrenamientoId { get; set; }
        public DateTime Inicio { get; set; }
        public DateTime? Fin { get; set; }
        public int? CaloriasQuemadas { get; set; }
        public int? FrecuenciaCardiacaMaxima { get; set; }
        public int? FrecuenciaCardiacaPromedio { get; set; }
        public List<SesionEjercicioRequestDto> Ejercicios { get; set; } = new List<SesionEjercicioRequestDto>();
    }
}
