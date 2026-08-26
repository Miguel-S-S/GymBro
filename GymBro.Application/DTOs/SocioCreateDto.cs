using System;
using System.Collections.Generic;
using System.Text;

namespace GymBro.Application.DTOs
{
    public class SocioCreateDto
    {
        public string Nombre { get; set; } = string.Empty;
        public string Apellido { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}
