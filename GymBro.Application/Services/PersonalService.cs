using GymBro.Application.Abstractions;
using GymBro.Application.DTOs.Acceso;
using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Domain.Exceptions;
using GymBro.Domain.Repositories;
using GymBro.Domain.Security;

namespace GymBro.Application.Services
{
    public interface IPersonalService
    {
        Task<EntrenadorResponseDto> CrearEntrenadorAsync(EntrenadorCreateDto dto);
        Task<IEnumerable<EntrenadorResponseDto>> ListarEntrenadoresAsync();
        Task AsignarSocioAsync(Guid entrenadorId, Guid socioId);
    }

    /// <summary>Gestión del personal del gimnasio (alta de Personal Trainers y asignación de socios).</summary>
    public class PersonalService : IPersonalService
    {
        private readonly IUsuarioRepository _usuarios;
        private readonly ISocioRepository _socios;
        private readonly IAsignacionRepository _asignaciones;
        private readonly IHasheadorContrasena _hasheador;
        private readonly IUsuarioActual _actual;
        private readonly IAuditService _audit;

        public PersonalService(IUsuarioRepository usuarios, ISocioRepository socios, IAsignacionRepository asignaciones,
            IHasheadorContrasena hasheador, IUsuarioActual actual, IAuditService audit)
        {
            _usuarios = usuarios;
            _socios = socios;
            _asignaciones = asignaciones;
            _hasheador = hasheador;
            _actual = actual;
            _audit = audit;
        }

        public async Task<EntrenadorResponseDto> CrearEntrenadorAsync(EntrenadorCreateDto dto)
        {
            var gimnasioId = _actual.GimnasioId ?? throw new AccesoDenegadoException();
            var email = AuthService.Normalizar(dto.Email);
            AuthService.ValidarEmail(email);
            AuthService.ValidarPassword(dto.Password);
            if (string.IsNullOrWhiteSpace(dto.NombreCompleto)) throw new ReglaNegocioException("El nombre es obligatorio.");
            if (await _usuarios.GetByEmailAsync(email) is not null)
                throw new ReglaNegocioException("Ya existe una cuenta con ese email.");

            var entrenador = new Usuario
            {
                Email = email,
                PasswordHash = _hasheador.Hash(dto.Password),
                NombreCompleto = dto.NombreCompleto.Trim(),
                Rol = RolUsuario.Entrenador,
                GimnasioId = gimnasioId
            };
            await _usuarios.AddAsync(entrenador);
            await _audit.RegistrarAsync("ALTA_ENTRENADOR", nameof(Usuario), entrenador.Id.ToString());
            return Mapear(entrenador);
        }

        public async Task<IEnumerable<EntrenadorResponseDto>> ListarEntrenadoresAsync() =>
            (await _usuarios.GetEntrenadoresAsync()).Select(Mapear);

        public async Task AsignarSocioAsync(Guid entrenadorId, Guid socioId)
        {
            var gimnasioId = _actual.GimnasioId ?? throw new AccesoDenegadoException();

            // Ambas lecturas pasan por el filtro de tenant: no se puede asignar nada de otro gimnasio.
            var entrenador = await _usuarios.GetByIdAsync(entrenadorId);
            if (entrenador is null || entrenador.Rol != RolUsuario.Entrenador || entrenador.GimnasioId != gimnasioId)
                throw new RecursoNoEncontradoException("Entrenador no encontrado.");

            var socio = await _socios.GetByIdAsync(socioId);
            if (socio is null || socio.GimnasioId != gimnasioId)
                throw new RecursoNoEncontradoException("Socio no encontrado en este gimnasio.");

            if (await _asignaciones.ExisteAsync(entrenadorId, socioId))
                throw new ReglaNegocioException("El socio ya está asignado a este entrenador.");

            await _asignaciones.AddAsync(new AsignacionEntrenadorSocio
            {
                GimnasioId = gimnasioId,
                EntrenadorUsuarioId = entrenadorId,
                SocioId = socioId
            });
            await _audit.RegistrarAsync("ASIGNACION_ENTRENADOR", nameof(AsignacionEntrenadorSocio), null, $"entrenador={entrenadorId}; socio={socioId}");
        }

        private static EntrenadorResponseDto Mapear(Usuario u) => new()
        {
            Id = u.Id,
            NombreCompleto = u.NombreCompleto,
            Email = u.Email,
            Activo = u.Activo
        };
    }
}