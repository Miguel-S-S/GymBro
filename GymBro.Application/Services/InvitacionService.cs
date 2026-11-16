using System.Security.Cryptography;
using System.Text;
using GymBro.Application.Abstractions;
using GymBro.Application.DTOs;
using GymBro.Application.DTOs.Acceso;
using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Domain.Exceptions;
using GymBro.Domain.Repositories;

namespace GymBro.Application.Services
{
    /// <summary>Generación y hash del código de invitación (compartido con el seeder de demostración).</summary>
    public static class CodigosInvitacion
    {
        /// <summary>256 bits aleatorios en Base64 apto para URL.</summary>
        public static string Generar() =>
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        /// <summary>SHA-256 en hexadecimal: es lo único que se guarda en la base.</summary>
        public static string Hash(string token) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    public interface IInvitacionService
    {
        /// <summary>Emite una invitación para la ficha (revoca las vigentes). Devuelve el código en claro, una sola vez.</summary>
        Task<InvitacionResponseDto> EmitirAsync(Socio socio, Guid gimnasioId);

        /// <summary>Revoca las invitaciones vigentes del socio. No guarda: lo confirma el SaveChanges de la misma unidad de trabajo.</summary>
        Task RevocarVigentesAsync(Guid socioId);

        /// <summary>Datos de la invitación para quien abre el enlace (sin sesión).</summary>
        Task<InvitacionInfoDto> ConsultarAsync(string token);

        /// <summary>El socio crea su contraseña, completa sus datos y acepta el consentimiento. Devuelve la sesión iniciada.</summary>
        Task<LoginResponse> ActivarAsync(ActivarInvitacionRequest request);
    }

    /// <summary>
    /// Alta presencial con invitación: el Dueño carga la ficha y el socio activa su propia cuenta,
    /// de modo que ninguna contraseña circula entre personas y el consentimiento lo da el titular (Ley 25.326).
    /// </summary>
    public class InvitacionService : IInvitacionService
    {
        public static readonly TimeSpan Vigencia = TimeSpan.FromDays(7);
        private const string MensajeInvalida = "La invitación no es válida o ya venció. Pedile al gimnasio que te genere una nueva.";

        private readonly IInvitacionRepository _invitaciones;
        private readonly IUsuarioRepository _usuarios;
        private readonly IHasheadorContrasena _hasheador;
        private readonly IGeneradorToken _tokens;
        private readonly IAuditService _audit;

        public InvitacionService(IInvitacionRepository invitaciones, IUsuarioRepository usuarios,
            IHasheadorContrasena hasheador, IGeneradorToken tokens, IAuditService audit)
        {
            _invitaciones = invitaciones;
            _usuarios = usuarios;
            _hasheador = hasheador;
            _tokens = tokens;
            _audit = audit;
        }

        public async Task<InvitacionResponseDto> EmitirAsync(Socio socio, Guid gimnasioId)
        {
            foreach (var vigente in await _invitaciones.GetVigentesDeSocioAsync(socio.Id))
                vigente.Revocar();

            var token = CodigosInvitacion.Generar();
            var invitacion = new InvitacionSocio
            {
                GimnasioId = gimnasioId,
                SocioId = socio.Id,
                TokenHash = CodigosInvitacion.Hash(token),
                ExpiraUtc = DateTime.UtcNow.Add(Vigencia)
            };
            await _invitaciones.AddAsync(invitacion); // guarda también las revocaciones anteriores
            await _audit.RegistrarAsync("EMISION_INVITACION", nameof(Socio), socio.Id.ToString());
            return new InvitacionResponseDto { Token = token, ExpiraUtc = invitacion.ExpiraUtc };
        }

        public async Task RevocarVigentesAsync(Guid socioId)
        {
            foreach (var vigente in await _invitaciones.GetVigentesDeSocioAsync(socioId))
                vigente.Revocar();
        }

        public async Task<InvitacionInfoDto> ConsultarAsync(string token)
        {
            var invitacion = await ObtenerVigenteAsync(token);
            return new InvitacionInfoDto
            {
                Nombre = invitacion.Socio.Nombre,
                Apellido = invitacion.Socio.Apellido,
                Email = invitacion.Socio.Email,
                Gimnasio = invitacion.Gimnasio.Nombre,
                ExpiraUtc = invitacion.ExpiraUtc
            };
        }

        public async Task<LoginResponse> ActivarAsync(ActivarInvitacionRequest r)
        {
            AuthService.ValidarPassword(r.Password);

            var genero = (r.Genero ?? string.Empty).Trim().ToUpperInvariant();
            if (genero is not ("M" or "F" or "X"))
                throw new ReglaNegocioException("El género debe ser M, F o X.");
            if (r.FechaNacimiento == default || r.FechaNacimiento > DateTime.UtcNow.AddYears(-14))
                throw new ReglaNegocioException("Fecha de nacimiento inválida.");
            if (!r.ConsentimientoDatosSalud)
                throw new ReglaNegocioException("Debe aceptar el tratamiento de sus datos de salud para usar la aplicación (Ley 25.326).");

            var invitacion = await ObtenerVigenteAsync(r.Token);
            var socio = invitacion.Socio;
            var email = AuthService.Normalizar(socio.Email);
            if (await _usuarios.GetByEmailAsync(email) is not null)
                throw new ReglaNegocioException("Ya existe una cuenta con ese email. Pedile al gimnasio que corrija tu ficha.");

            socio.Genero = genero;
            socio.FechaNacimiento = DateTime.SpecifyKind(r.FechaNacimiento.Date, DateTimeKind.Utc);
            socio.ConsentimientoDatosSaludFecha = DateTime.UtcNow;
            socio.FechaModificacion = DateTime.UtcNow;
            invitacion.MarcarUsada();

            // La cuenta del socio no lleva gimnasio (restricción CK_Usuario_Rol_Alcance): la pertenencia vive en Socio.GimnasioId.
            var usuario = new Usuario
            {
                Email = email,
                PasswordHash = _hasheador.Hash(r.Password),
                NombreCompleto = socio.NombreCompleto,
                Rol = RolUsuario.Socio,
                SocioId = socio.Id
            };

            // Una sola transacción: cuenta nueva + ficha completada + invitación usada (todo está en el mismo DbContext).
            await _usuarios.AddAsync(usuario);
            await _audit.RegistrarAsync("ACTIVACION_INVITACION", nameof(Socio), socio.Id.ToString());
            return _tokens.Generar(usuario);
        }

        private async Task<InvitacionSocio> ObtenerVigenteAsync(string? token)
        {
            if (string.IsNullOrWhiteSpace(token)) throw new ReglaNegocioException(MensajeInvalida);

            var invitacion = await _invitaciones.GetByTokenHashAsync(CodigosInvitacion.Hash(token.Trim()));
            if (invitacion is null || !invitacion.EstaVigente(DateTime.UtcNow) || invitacion.Socio.FechaBaja is not null)
                throw new ReglaNegocioException(MensajeInvalida); // mismo mensaje para inexistente, usada, revocada o vencida
            return invitacion;
        }
    }
}
