using GymBro.Application.Services;
using GymBro.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymBro.Api.Controllers
{
    /// <summary>
    /// Alertas y sugerencias de las reglas de seguridad (Etapa 10a). Son datos de salud: el Administrador de la Plataforma
    /// no accede (403, decisión vigente). Un recurso no visible responde 404, como en el resto de la API.
    /// </summary>
    [ApiController]
    [Route("api/alertas")]
    [Authorize(Roles = Roles.AdministradorGimnasio + "," + Roles.Entrenador + "," + Roles.Socio)]
    public class AlertasController : ControllerBase
    {
        private readonly IAlertasService _alertas;
        public AlertasController(IAlertasService alertas) { _alertas = alertas; }

        /// <summary>estado: Pendiente o Vista (opcional).</summary>
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] Guid? socioId, [FromQuery] string? estado) =>
            Ok(await _alertas.ListarAsync(socioId, estado));

        [HttpPut("{id:guid}/vista")]
        public async Task<IActionResult> MarcarVista(Guid id) => Ok(await _alertas.MarcarVistaAsync(id));
    }
}
