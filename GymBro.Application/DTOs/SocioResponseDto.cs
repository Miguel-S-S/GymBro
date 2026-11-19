using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace GymBro.Application.DTOs
{
    public class SocioResponseDto
    {
        public Guid Id { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime FechaAlta { get; set; }
        public Guid? GimnasioId { get; set; }

        /// <summary>Activo (ya tiene cuenta), Invitado (invitación vigente) o SinAcceso (invitación vencida o revocada).</summary>
        public string EstadoAcceso { get; set; } = EstadosAcceso.SinAcceso;

        /// <summary>Estado administrativo de la ficha (Etapa 9a-2b): Activo o Inactivo. Informativo.</summary>
        public string Estado { get; set; } = "Activo";
    }

    public static class EstadosAcceso
    {
        public const string Activo = "Activo";
        public const string Invitado = "Invitado";
        public const string SinAcceso = "SinAcceso";
    }
}