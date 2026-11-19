using GymBro.Application.DTOs;
using GymBro.Application.Services;
using GymBro.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymBro.Api.Controllers
{
    /// <summary>
    /// Equipamiento del gimnasio (Etapa 9a-2). Lectura: Administrador de la Plataforma (cualquier gimnasio, indicando
    /// gimnasioId), Dueño y Personal Trainer (el propio). Escritura: solo el Dueño, sobre su gimnasio (decisión D2:
    /// el Administrador de la Plataforma solo lee en esta etapa). No contiene datos de salud.
    /// </summary>
    [ApiController]
    [Route("api/equipamiento")]
    public class EquipamientoController : ControllerBase
    {
        private const string Lectores = Roles.SuperAdministrador + "," + Roles.PersonalGimnasio;

        private readonly IEquipamientoService _equipamiento;
        public EquipamientoController(IEquipamientoService equipamiento) { _equipamiento = equipamiento; }

        [HttpGet]
        [Authorize(Roles = Lectores)]
        public async Task<IActionResult> Get([FromQuery] Guid? gimnasioId) => Ok(await _equipamiento.ObtenerAsync(gimnasioId));

        /// <summary>Reemplaza el conjunto marcado (lista de tipo y cantidad opcional).</summary>
        [HttpPut]
        [Authorize(Roles = Roles.AdministradorGimnasio)]
        public async Task<IActionResult> Put([FromBody] EquipamientoReemplazoRequest request) =>
            Ok(await _equipamiento.ReemplazarAsync(request));

        /// <summary>Crea un tipo propio del gimnasio (la clave se genera).</summary>
        [HttpPost("tipos")]
        [Authorize(Roles = Roles.AdministradorGimnasio)]
        public async Task<IActionResult> PostTipo([FromBody] TipoEquipamientoCreateRequest request) =>
            StatusCode(StatusCodes.Status201Created, await _equipamiento.CrearTipoAsync(request));
    }
}
