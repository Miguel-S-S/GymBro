using System;

namespace GymBro.Application.DTOs
{
    public class EjercicioResumenDto
    {
        public Guid Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string ZonaObjetivo { get; set; } = string.Empty;
        public string MusculoPrincipal { get; set; } = string.Empty;
        public string Equipamiento { get; set; } = string.Empty;
        public int Dificultad { get; set; }
    }

    public class EjercicioDetalleDto
    {
        public Guid Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Equipamiento { get; set; } = string.Empty;
        public string ZonaObjetivo { get; set; } = string.Empty;
        public string MusculoPrincipal { get; set; } = string.Empty;
        public string Sinergistas { get; set; } = string.Empty;
        public string Estabilizadores { get; set; } = string.Empty;
        public string Preparacion { get; set; } = string.Empty;
        public string Ejecucion { get; set; } = string.Empty;
        public int Dificultad { get; set; }
    }
}
