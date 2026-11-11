using GymBro.Application.Abstractions;
using GymBro.Application.DTOs.Acceso;
using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Domain.Exceptions;
using GymBro.Domain.Repositories;
using GymBro.Domain.Security;

namespace GymBro.Application.Services
{
    public interface IGimnasioService
    {
        Task<GimnasioResponseDto> CrearAsync(GimnasioCreateDto dto);
        Task<IEnumerable<GimnasioResponseDto>> ListarAsync();
        Task<GimnasioResponseDto> ObtenerPropioAsync();
        Task<GimnasioResponseDto> ActualizarPoliticaRevisionAsync(bool requiereAprobacion);
    }

    public class GimnasioService : IGimnasioService
    {
        private readonly IGimnasioRepository _gimnasios;
        private readonly IUsuarioRepository _usuarios;
        private readonly IHasheadorContrasena _hasheador;
        private readonly IUsuarioActual _actual;
        private readonly IAuditService _audit;

        public GimnasioService(IGimnasioRepository gimnasios, IUsuarioRepository usuarios, IHasheadorContrasena hasheador,
            IUsuarioActual actual, IAuditService audit)
        {
            _gimnasios = gimnasios;
            _usuarios = usuarios;
            _hasheador = hasheador;
            _actual = actual;
            _audit = audit;
        }

        /// <summary>Alta de una institución y de su Dueño (tras validación externa de la solicitud). Solo SuperAdministrador.</summary>
        public async Task<GimnasioResponseDto> CrearAsync(GimnasioCreateDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Nombre)) throw new ReglaNegocioException("El nombre del gimnasio es obligatorio.");
            if (string.IsNullOrWhiteSpace(dto.DuenoNombreCompleto)) throw new ReglaNegocioException("El nombre del dueño es obligatorio.");

            var email = AuthService.Normalizar(dto.DuenoEmail);
            AuthService.ValidarEmail(email);
            AuthService.ValidarPassword(dto.DuenoPassword);
            if (await _usuarios.GetByEmailAsync(email) is not null)
                throw new ReglaNegocioException("Ya existe una cuenta con ese email.");

            var gimnasio = new Gimnasio
            {
                Nombre = dto.Nombre.Trim(),
                Codigo = await GenerarCodigoAsync(),
                RequiereAprobacionRutinas = dto.RequiereAprobacionRutinas
            };
            var dueno = new Usuario
            {
                Email = email,
                PasswordHash = _hasheador.Hash(dto.DuenoPassword),
                NombreCompleto = dto.DuenoNombreCompleto.Trim(),
                Rol = RolUsuario.AdministradorGimnasio,
                GimnasioId = gimnasio.Id
            };

            await _gimnasios.AddAsync(gimnasio, dueno);
            await _audit.RegistrarAsync("ALTA_GIMNASIO", nameof(Gimnasio), gimnasio.Id.ToString(), gimnasio.Nombre);
            return Mapear(gimnasio);
        }

        public async Task<IEnumerable<GimnasioResponseDto>> ListarAsync() =>
            (await _gimnasios.GetAllAsync()).Select(Mapear);

        public async Task<GimnasioResponseDto> ObtenerPropioAsync()
        {
            var gimnasio = await _gimnasios.GetByIdAsync(GimnasioActual());
            return Mapear(gimnasio ?? throw new RecursoNoEncontradoException("Gimnasio no encontrado."));
        }

        public async Task<GimnasioResponseDto> ActualizarPoliticaRevisionAsync(bool requiereAprobacion)
        {
            var gimnasio = await _gimnasios.GetByIdAsync(GimnasioActual())
                ?? throw new RecursoNoEncontradoException("Gimnasio no encontrado.");
            gimnasio.RequiereAprobacionRutinas = requiereAprobacion;
            gimnasio.FechaModificacion = DateTime.UtcNow;
            await _gimnasios.SaveChangesAsync();
            await _audit.RegistrarAsync("POLITICA_REVISION", nameof(Gimnasio), gimnasio.Id.ToString(), $"RequiereAprobacion={requiereAprobacion}");
            return Mapear(gimnasio);
        }

        private Guid GimnasioActual() =>
            _actual.GimnasioId ?? throw new AccesoDenegadoException("El usuario no pertenece a un gimnasio.");

        private async Task<string> GenerarCodigoAsync()
        {
            for (var i = 0; i < 10; i++)
            {
                var codigo = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
                if (!await _gimnasios.ExisteCodigoAsync(codigo)) return codigo;
            }
            throw new InvalidOperationException("No se pudo generar un código de gimnasio único.");
        }

        private static GimnasioResponseDto Mapear(Gimnasio g) => new()
        {
            Id = g.Id,
            Nombre = g.Nombre,
            Codigo = g.Codigo,
            Activo = g.Activo,
            RequiereAprobacionRutinas = g.RequiereAprobacionRutinas,
            FechaAlta = g.FechaAlta
        };
    }
}