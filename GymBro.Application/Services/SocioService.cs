using GymBro.Application.DTOs;
using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Domain.Exceptions;
using GymBro.Domain.Repositories;
using GymBro.Domain.Security;

namespace GymBro.Application.Services
{
    public class SocioService : ISocioService
    {
        private readonly ISocioRepository _repository;
        private readonly ISocioAccessService _acceso;
        private readonly IUsuarioActual _actual;
        private readonly IAuditService _audit;

        public SocioService(ISocioRepository repository, ISocioAccessService acceso, IUsuarioActual actual, IAuditService audit)
        {
            _repository = repository;
            _acceso = acceso;
            _actual = actual;
            _audit = audit;
        }

        public async Task<IEnumerable<SocioResponseDto>> GetAllSociosAsync()
        {
            // El alcance depende del rol; el aislamiento entre gimnasios lo garantiza el filtro del DbContext.
            IEnumerable<Socio> socios = _actual.Rol switch
            {
                RolUsuario.Entrenador => await _repository.GetByEntrenadorAsync(_actual.UsuarioId ?? throw new AccesoDenegadoException()),
                RolUsuario.Socio => await PropioAsync(),
                RolUsuario.AdministradorGimnasio => await _repository.GetAllAsync(),
                _ => _actual.EsProcesoDeSistema ? await _repository.GetAllAsync() : throw new AccesoDenegadoException()
            };

            return socios.Select(Mapear);
        }

        public async Task<SocioResponseDto> CreateSocioAsync(SocioCreateDto dto)
        {
            // Alta presencial: solo el Dueño, siempre dentro de su gimnasio.
            var gimnasioId = _actual.GimnasioId ?? throw new AccesoDenegadoException();
            if (_actual.Rol != RolUsuario.AdministradorGimnasio) throw new AccesoDenegadoException();

            if (string.IsNullOrWhiteSpace(dto.Nombre) || string.IsNullOrWhiteSpace(dto.Apellido))
                throw new ReglaNegocioException("Nombre y apellido son obligatorios.");

            string? dni = null;
            if (!string.IsNullOrWhiteSpace(dto.Dni))
            {
                dni = dto.Dni.Replace(".", "").Trim();
                if (dni.Length is < 7 or > 9 || !dni.All(char.IsDigit))
                    throw new ReglaNegocioException("El DNI debe tener entre 7 y 9 dígitos.");
                if (await _repository.ExisteDniAsync(dni))
                    throw new ReglaNegocioException("Ya existe un socio registrado con ese DNI.");
            }

            var socio = new Socio
            {
                Nombre = dto.Nombre.Trim(),
                Apellido = dto.Apellido.Trim(),
                Email = dto.Email.Trim().ToLowerInvariant(),
                Dni = dni,
                GimnasioId = gimnasioId
            };

            var nuevoSocio = await _repository.AddAsync(socio);
            await _audit.RegistrarAsync("ALTA_SOCIO", nameof(Socio), nuevoSocio.Id.ToString());
            return Mapear(nuevoSocio);
        }

        public async Task<SocioResponseDto?> GetSocioByIdAsync(Guid id)
        {
            await _acceso.AsegurarAccesoAsync(id);
            var socio = await _repository.GetByIdAsync(id);
            return socio is null ? null : Mapear(socio);
        }

        private async Task<IEnumerable<Socio>> PropioAsync()
        {
            var id = _actual.SocioId ?? throw new AccesoDenegadoException();
            var socio = await _repository.GetByIdAsync(id);
            return socio is null ? Enumerable.Empty<Socio>() : new[] { socio };
        }

        private static SocioResponseDto Mapear(Socio s) => new()
        {
            Id = s.Id,
            NombreCompleto = s.NombreCompleto,
            Email = s.Email,
            FechaAlta = s.FechaAlta,
            GimnasioId = s.GimnasioId
        };
    }
}