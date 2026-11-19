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

        /// <summary>Visible solo para el Dueño del gimnasio y para el propio socio.</summary>
        public string? Dni { get; set; }

        /// <summary>Activo, Invitado o SinAcceso (ver EstadosAcceso). Mientras no sea Activo, el Dueño puede corregir la ficha.</summary>
        public string EstadoAcceso { get; set; } = EstadosAcceso.SinAcceso;

        /// <summary>Null cuando el socio se dio de alta sin informar la fecha.</summary>
        public DateTime? FechaNacimiento { get; set; }

        /// <summary>Nombre del enum ObjetivoEntrenamiento; null si no fue asignado.</summary>
        public string? Objetivo { get; set; }

        /// <summary>Nombre del enum NivelExperiencia; null si no fue asignado.</summary>
        public string? Nivel { get; set; }

        public DateTime FechaAlta { get; set; }

        /// <summary>Estado administrativo de la ficha (Etapa 9a-2b): Activo o Inactivo. Informativo.</summary>
        public string Estado { get; set; } = "Activo";
    }

    /// <summary>Cambio del estado administrativo del socio (Etapa 9a-2b): "Activo" o "Inactivo".</summary>
    public class EstadoSocioRequest
    {
        public string Estado { get; set; } = string.Empty;
    }
}
