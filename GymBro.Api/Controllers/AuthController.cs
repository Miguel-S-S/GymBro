using GymBro.Application.DTOs.Acceso;
using GymBro.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GymBro.Api.Controllers
{
    [ApiController]
    [Route("api/auth")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _auth;
        private readonly IInvitacionService _invitaciones;
        public AuthController(IAuthService auth, IInvitacionService invitaciones) { _auth = auth; _invitaciones = invitaciones; }

        [HttpPost("login")]
        public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request) =>
                Ok(await _auth.LoginAsync(request));

        /// <summary>Alta autogestionada del socio desde la PWA (queda como socio independiente hasta vincularse).</summary>
        [HttpPost("registro-socio")]
        public async Task<ActionResult<LoginResponse>> RegistrarSocio([FromBody] RegistroSocioRequest request) =>
Ok(await _auth.RegistrarSocioAsync(request));

        // El código viaja en el cuerpo (POST) y no en la URL para que no quede en registros de acceso.

        /// <summary>Datos de una invitación de alta presencial, para quien abre el enlace.</summary>
        [HttpPost("invitacion/consultar")]
        public async Task<ActionResult<InvitacionInfoDto>> ConsultarInvitacion([FromBody] ConsultarInvitacionRequest request) =>
            Ok(await _invitaciones.ConsultarAsync(request.Token));

        /// <summary>El socio crea su contraseña y completa sus datos; queda con la sesión iniciada.</summary>
        [HttpPost("invitacion/activar")]
        public async Task<ActionResult<LoginResponse>> ActivarInvitacion([FromBody] ActivarInvitacionRequest request) =>
            Ok(await _invitaciones.ActivarAsync(request));
    }
}
