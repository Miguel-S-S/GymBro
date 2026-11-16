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

        /// <summary>Obligatorio: identifica al socio en toda la plataforma y habilita la validación cruzada de vinculación.</summary>
        public string Dni { get; set; } = string.Empty;
    }

    /// <summary>Corrección de la ficha de un socio que todavía no activó su cuenta.</summary>
    public class SocioUpdateDto
    {
        public string Nombre { get; set; } = string.Empty;
        public string Apellido { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Dni { get; set; } = string.Empty;
    }

    /// <summary>Código de invitación en claro: se muestra una sola vez, al emitirlo.</summary>
    public class InvitacionResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiraUtc { get; set; }
    }

    public class SocioAltaResponseDto
    {
        public SocioResponseDto Socio { get; set; } = null!;
        public InvitacionResponseDto Invitacion { get; set; } = null!;
    }
}
