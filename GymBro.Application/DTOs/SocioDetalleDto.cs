using System;

namespace GymBro.Application.DTOs
{
    public class SocioDetalleDto
    {
        public Guid Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Apellido { get; set; } = string.Empty;
        public string NombreCompleto { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Genero { get; set; } = string.Empty;

        /// <summary>Null cuando el socio se dio de alta sin informar la fecha.</summary>
        public DateTime? FechaNacimiento { get; set; }

        /// <summary>Nombre del enum ObjetivoEntrenamiento; null si no fue asignado.</summary>
        public string? Objetivo { get; set; }

        /// <summary>Nombre del enum NivelExperiencia; null si no fue asignado.</summary>
        public string? Nivel { get; set; }

        public DateTime FechaAlta { get; set; }
    }
}
