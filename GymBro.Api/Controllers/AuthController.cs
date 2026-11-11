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
        public AuthController(IAuthService auth) { _auth = auth; }

        [HttpPost("login")]
        public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request) =>
                Ok(await _auth.LoginAsync(request));

        /// <summary>Alta autogestionada del socio desde la PWA (queda como socio independiente hasta vincularse).</summary>
        [HttpPost("registro-socio")]
        public async Task<ActionResult<LoginResponse>> RegistrarSocio([FromBody] RegistroSocioRequest request) =>
Ok(await _auth.RegistrarSocioAsync(request));
    }
}