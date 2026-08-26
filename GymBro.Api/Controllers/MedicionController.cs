using GymBro.Application.Services;
using GymBro.Application.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace GymBro.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MedicionController : ControllerBase
    {
        private readonly IMedicionService _medicionService;

        public MedicionController(IMedicionService medicionService)
        {
            _medicionService = medicionService;
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] MedicionCreateDto dto)
        {
            await _medicionService.RegistrarMedicionAsync(dto);
            return Ok(new {Mensaje = "Medicion Registrada con Exito!"});
        }
    }
}

