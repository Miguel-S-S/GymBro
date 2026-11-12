using Microsoft.AspNetCore.Mvc;
using GymBro.Application.Services;
using GymBro.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
namespace GymBro.Api.Controllers
{
    [ApiController]
    [Route("api/auditoria")]
    [Authorize(Roles = Roles.SuperAdministrador)]
    public class AuditoriaController : ControllerBase
    {
        private readonly IAuditService _audit;
        public AuditoriaController(IAuditService audit) { _audit = audit; }

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] int cantidad = 100, [FromQuery] Guid? gimnasioId = null,
            [FromQuery] DateTime? desde = null, [FromQuery] DateTime? hasta = null,
            [FromQuery] string? accion = null, [FromQuery] string? entidad = null) =>
            Ok(await _audit.ConsultarAsync(cantidad, gimnasioId,
                desde?.ToUniversalTime(), hasta?.ToUniversalTime(), accion, entidad));
    }
}