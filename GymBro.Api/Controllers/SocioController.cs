
using GymBro.Application.DTOs;
using GymBro.Application.Services;
using GymBro.Domain.Entities;
using GymBro.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace GymBro.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
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

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] SocioCreateDto socio)
        {
            //var nuevoSocio = await _repository.AddAsync(socio);
            var nuevoSocio = await _socioService.CreateSocioAsync(socio);
            return CreatedAtAction(nameof(GetById), new { id = nuevoSocio.Id }, nuevoSocio);
        }
    }
}
