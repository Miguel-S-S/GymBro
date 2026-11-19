
using GymBro.Application.DTOs;
using GymBro.Application.Services;
using GymBro.Domain.Entities;
using GymBro.Domain.Repositories;
using GymBro.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymBro.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = Roles.AdministradorGimnasio + "," + Roles.Entrenador + "," + Roles.Socio)]
    public class SocioController : ControllerBase
    {
        //private readonly ISocioRepository _repository;
        private readonly ISocioService _socioService;

        //public SociosController(ISocioRepository repository)
        //{
        //    _repository = repository;
        //}

        public SocioController(ISocioService socioService)
        {
            _socioService = socioService;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            //var socios = await _repository.GetAllAsync();
            var socios = await _socioService.GetAllSociosAsync();
            return Ok(socios);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var socio = await _socioService.GetSocioByIdAsync(id);
            if (socio == null)
            {
                return NotFound();
            }

            return Ok(socio);
        }

        /// <summary>Alta presencial: crea la ficha y devuelve el código de invitación (se muestra una sola vez).</summary>
        [HttpPost]
        [Authorize(Roles = Roles.AdministradorGimnasio)]
        public async Task<IActionResult> Post([FromBody] SocioCreateDto socio)
        {
            var alta = await _socioService.CreateSocioAsync(socio);
            return CreatedAtAction(nameof(GetById), new { id = alta.Socio.Id }, alta);
        }

        /// <summary>Corrige la ficha de un socio que todavía no activó su cuenta.</summary>
        [HttpPut("{id:guid}")]
        [Authorize(Roles = Roles.AdministradorGimnasio)]
        public async Task<IActionResult> Put(Guid id, [FromBody] SocioUpdateDto dto) =>
            Ok(await _socioService.ActualizarSocioAsync(id, dto));

        /// <summary>Emite una invitación nueva; la anterior deja de valer.</summary>
        /// <summary>Etapa 9a-2b: estado administrativo de la ficha (Activo o Inactivo), informativo. Solo el Dueño.</summary>
        [HttpPut("{id:guid}/estado")]
        [Authorize(Roles = Roles.AdministradorGimnasio)]
        public async Task<IActionResult> PutEstado(Guid id, [FromBody] EstadoSocioRequest request) =>
            Ok(await _socioService.CambiarEstadoAsync(id, request.Estado));

        [HttpPost("{id:guid}/invitacion")]
        [Authorize(Roles = Roles.AdministradorGimnasio)]
        public async Task<IActionResult> RenovarInvitacion(Guid id) =>
            Ok(await _socioService.RenovarInvitacionAsync(id));

        /// <summary>Ficha sin cuenta: baja lógica. Socio con cuenta: se desvincula del gimnasio.</summary>
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = Roles.AdministradorGimnasio)]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _socioService.BajaSocioAsync(id);
            return NoContent();
        }
    }
}
