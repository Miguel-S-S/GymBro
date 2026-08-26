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
    }
}
