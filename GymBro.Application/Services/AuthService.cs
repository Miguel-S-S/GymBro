using System.Net.Mail;
using GymBro.Application.Abstractions;
using GymBro.Application.DTOs.Acceso;
using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Domain.Exceptions;
using GymBro.Domain.Repositories;
namespace GymBro.Application.Services
{
    public interface IAuthService
    {
        Task<LoginResponse> LoginAsync(LoginRequest request);
        /// <summary>Alta autogestionada desde la PWA: crea el Socio y su cuenta (socio independiente hasta vincularse).</summary>
        Task<LoginResponse> RegistrarSocioAsync(RegistroSocioRequest request);
    }

    public class AuthService : IAuthService
    {
        private readonly IUsuarioRepository _usuarios;
        private readonly ISocioRepository _socios;
        private readonly IHasheadorContrasena _hasheador;
        private readonly IGeneradorToken _tokens;
        private readonly IAuditService _audit;
        private readonly IGimnasioRepository _gimnasios;

        public AuthService(IUsuarioRepository usuarios, ISocioRepository socios, IHasheadorContrasena hasheador,
            IGeneradorToken tokens, IAuditService audit, IGimnasioRepository gimnasios)
        {
            _gimnasios = gimnasios;
            _usuarios = usuarios;
            _socios = socios;
            _hasheador = hasheador;
            _tokens = tokens;
            _audit = audit;
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request)
        {
            var email = Normalizar(request.Email);
            var usuario = await _usuarios.GetByEmailAsync(email);

            // Mismo mensaje para usuario inexistente y contraseña errónea: no revela qué cuentas existen.
            if (usuario is null || !usuario.Activo || !_hasheador.Verificar(usuario.PasswordHash, request.Password ?? string.Empty))
            {
                await _audit.RegistrarAsync("LOGIN_FALLIDO", nameof(Usuario), null, email);
                throw new NoAutenticadoException();
            }

            // Personal de un gimnasio deshabilitado por el Administrador de la Plataforma: se trata como credenciales inválidas.
            if (usuario.Rol is RolUsuario.AdministradorGimnasio or RolUsuario.Entrenador
                && usuario.GimnasioId is Guid gimnasioId
                && !await _gimnasios.EstaActivoAsync(gimnasioId))
            {
                await _audit.RegistrarAsync("LOGIN_GIMNASIO_INACTIVO", nameof(Usuario), usuario.Id.ToString(), email);
                throw new NoAutenticadoException();
            }

            return _tokens.Generar(usuario);
        }

        public async Task<LoginResponse> RegistrarSocioAsync(RegistroSocioRequest r)
        {
            var email = Normalizar(r.Email);
            ValidarEmail(email);
            ValidarPassword(r.Password);

            if (string.IsNullOrWhiteSpace(r.Nombre) || string.IsNullOrWhiteSpace(r.Apellido))
                throw new ReglaNegocioException("Nombre y apellido son obligatorios.");

            var dni = (r.Dni ?? string.Empty).Replace(".", "").Trim();
            if (dni.Length is < 7 or > 9 || !dni.All(char.IsDigit))
                throw new ReglaNegocioException("El DNI debe tener entre 7 y 9 dígitos.");

            var genero = (r.Genero ?? string.Empty).Trim().ToUpperInvariant();
            if (genero is not ("M" or "F" or "X"))
                throw new ReglaNegocioException("El género debe ser M, F o X.");

            if (r.FechaNacimiento == default || r.FechaNacimiento > DateTime.UtcNow.AddYears(-14))
                throw new ReglaNegocioException("Fecha de nacimiento inválida.");

            if (!r.ConsentimientoDatosSalud)
                throw new ReglaNegocioException("Debe aceptar el tratamiento de sus datos de salud para usar la aplicación (Ley 25.326).");

            if (await _usuarios.GetByEmailAsync(email) is not null)
                throw new ReglaNegocioException("Ya existe una cuenta con ese email.");
            if (await _socios.ExisteDniAsync(dni))
                throw new ReglaNegocioException("Ya existe un socio registrado con ese DNI.");

            var socio = new Socio
            {
                Id = Guid.NewGuid(),
                Nombre = r.Nombre.Trim(),
                Apellido = r.Apellido.Trim(),
                Email = email,
                Dni = dni,
                Genero = genero,
                FechaNacimiento = DateTime.SpecifyKind(r.FechaNacimiento.Date, DateTimeKind.Utc),
                Objetivo = ObjetivoEntrenamiento.Mantenimiento,
                Nivel = NivelExperiencia.Principiante,
                ConsentimientoDatosSaludFecha = DateTime.UtcNow
            };
            var usuario = new Usuario
            {
                Email = email,
                PasswordHash = _hasheador.Hash(r.Password),
                NombreCompleto = socio.NombreCompleto,
                Rol = RolUsuario.Socio,
                Socio = socio,
                SocioId = socio.Id
            };

            // Una sola operación: Socio  Usuario se insertan en la misma transacción.
            await _usuarios.AddAsync(usuario);
            await _audit.RegistrarAsync("REGISTRO_SOCIO", nameof(Socio), socio.Id.ToString());
            return _tokens.Generar(usuario);
        }

        internal static string Normalizar(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();

        internal static void ValidarEmail(string email)
        {
            if (!MailAddress.TryCreate(email, out var m) || m.Address != email)
                throw new ReglaNegocioException("Email inválido.");
        }

        internal static void ValidarPassword(string? password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < 8)
                throw new ReglaNegocioException("La contraseña debe tener al menos 8 caracteres.");
        }
    }
}