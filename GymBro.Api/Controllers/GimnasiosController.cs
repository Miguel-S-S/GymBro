using GymBro.Application.DTOs.Acceso;
using Microsoft.AspNetCore.Mvc;
using GymBro.Application.Services;
using GymBro.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymBro.Api.Controllers
{
    /// <summary>Gestión SaaS de instituciones: exclusivo del Administrador de la Plataforma.</summary>
    [ApiController]
    [Route("api/gimnasios")]
    [Authorize(Roles = Roles.SuperAdministrador)]
    public class GimnasiosController : ControllerBase
    {
        private readonly IGimnasioService _gimnasios;
        public GimnasiosController(IGimnasioService gimnasios) { _gimnasios = gimnasios; }

        [HttpGet]
        public async Task<IActionResult> Get() => Ok(await _gimnasios.ListarAsync());

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] GimnasioCreateDto dto) =>
StatusCode(StatusCodes.Status201Created, await _gimnasios.CrearAsync(dto));
    }

    /// <summary>Configuración del propio gimnasio: Dueño de Gimnasio.</summary>
    [ApiController]
    [Route("api/mi-gimnasio")]
    [Authorize(Roles = Roles.AdministradorGimnasio)]
    public class MiGimnasioController : ControllerBase
    {
        private readonly IGimnasioService _gimnasios;
        public MiGimnasioController(IGimnasioService gimnasios) { _gimnasios = gimnasios; }

        [HttpGet]
        public async Task<IActionResult> Get() => Ok(await _gimnasios.ObtenerPropioAsync());

        /// <summary>Activa/desactiva la revisión humana obligatoria de las rutinas generadas por la IA.</summary>
        [HttpPut("politica-revision")]
        public async Task<IActionResult> PutPolitica([FromBody] PoliticaRevisionRequest request) =>
Ok(await _gimnasios.ActualizarPoliticaRevisionAsync(request.RequiereAprobacionRutinas));
    }
}