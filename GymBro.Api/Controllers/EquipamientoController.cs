using GymBro.Application.DTOs;
using GymBro.Application.Services;
using GymBro.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymBro.Api.Controllers
{
    /// <summary>
    /// Equipamiento del gimnasio (Etapa 9a-2). Lectura: Administrador de la Plataforma (cualquier gimnasio, indicando
    /// gimnasioId), Dueño y Personal Trainer (el propio). Escritura (Etapa 9a-2b, D2): el Dueño sobre su gimnasio y el
    /// Administrador de la Plataforma sobre el gimnasio indicado en el cuerpo; los tipos globales, solo la plataforma.
    /// No contiene datos de salud.
    /// </summary>
    [ApiController]
    [Route("api/equipamiento")]
    public class EquipamientoController : ControllerBase
    {
        private const string Lectores = Roles.SuperAdministrador + "," + Roles.PersonalGimnasio;
        private const string Escritores = Roles.SuperAdministrador + "," + Roles.AdministradorGimnasio;

        private readonly IEquipamientoService _equipamiento;
        public EquipamientoController(IEquipamientoService equipamiento) { _equipamiento = equipamiento; }

        [HttpGet]
        [Authorize(Roles = Lectores)]
        public async Task<IActionResult> Get([FromQuery] Guid? gimnasioId) => Ok(await _equipamiento.ObtenerAsync(gimnasioId));

        /// <summary>Reemplaza el conjunto marcado (lista de tipo y cantidad opcional).</summary>
        [HttpPut]
        [Authorize(Roles = Escritores)]
        public async Task<IActionResult> Put([FromBody] EquipamientoReemplazoRequest request) =>
            Ok(await _equipamiento.ReemplazarAsync(request));

        /// <summary>
        /// Etapa 9a-2b (O1): con el mismo cuerpo que el PUT, informa qué ejercicios propios activos quedarían sin poder
        /// usarse en el baseline. Solo lectura: no modifica datos.
        /// </summary>
        [HttpPost("previsualizacion")]
        [Authorize(Roles = Escritores)]
        public async Task<IActionResult> Previsualizar([FromBody] EquipamientoReemplazoRequest request) =>
            Ok(await _equipamiento.PrevisualizarAsync(request));

        /// <summary>Crea un tipo propio del gimnasio (la clave se genera).</summary>
        [HttpPost("tipos")]
        [Authorize(Roles = Escritores)]
        public async Task<IActionResult> PostTipo([FromBody] TipoEquipamientoCreateRequest request) =>
            StatusCode(StatusCodes.Status201Created, await _equipamiento.CrearTipoAsync(request));

        /// <summary>Etapa 9a-2b: renombra un tipo (la clave no cambia).</summary>
        [HttpPut("tipos/{id:guid}")]
        [Authorize(Roles = Escritores)]
        public async Task<IActionResult> PutTipo(Guid id, [FromBody] TipoEquipamientoEditRequest request) =>
            Ok(await _equipamiento.EditarTipoAsync(id, request));

        /// <summary>Etapa 9a-2b: baja lógica de un tipo que ningún ejercicio usa; se desmarca en los gimnasios.</summary>
        [HttpDelete("tipos/{id:guid}")]
        [Authorize(Roles = Escritores)]
        public async Task<IActionResult> DeleteTipo(Guid id)
        {
            await _equipamiento.DarDeBajaTipoAsync(id);
            return NoContent();
        }
    }
}
