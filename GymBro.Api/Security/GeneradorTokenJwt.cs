using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GymBro.Application.Abstractions;
using GymBro.Application.DTOs.Acceso;
using GymBro.Domain.Entities;
using Microsoft.IdentityModel.Tokens;

namespace GymBro.Api.Security
{
    public class JwtOptions
    {
        public const string Seccion = "Jwt";
        public string Key { get; set; } = string.Empty;
        public string Issuer { get; set; } = "GymBro.Api";
        public string Audience { get; set; } = "GymBro.Clientes";
        public int ExpiraMinutos { get; set; } = 120;
    }

    public class GeneradorTokenJwt : IGeneradorToken
    {
        private readonly JwtOptions _opciones;
        public GeneradorTokenJwt(Microsoft.Extensions.Options.IOptions<JwtOptions> opciones) { _opciones = opciones.Value; }

        public LoginResponse Generar(Usuario usuario)
        {
            var claims = new List<Claim>
            {
                new (ClaimNames.Sub, usuario.Id.ToString()),
                new (ClaimNames.Role, usuario.Rol.ToString()),
                new (ClaimNames.Name, usuario.NombreCompleto),
            };
            if (usuario.GimnasioId is Guid g) claims.Add(new Claim(ClaimNames.GimnasioId, g.ToString()));
            if (usuario.SocioId is Guid s) claims.Add(new Claim(ClaimNames.SocioId, s.ToString()));

            var expira = DateTime.UtcNow.AddMinutes(_opciones.ExpiraMinutos);
            var credenciales = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opciones.Key)), SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(_opciones.Issuer, _opciones.Audience, claims,
            notBefore: DateTime.UtcNow, expires: expira, signingCredentials: credenciales);

            return new LoginResponse
            {
                Token = new JwtSecurityTokenHandler().WriteToken(token),
                ExpiraUtc = expira,
                Rol = usuario.Rol.ToString(),
                NombreCompleto = usuario.NombreCompleto,
                GimnasioId = usuario.GimnasioId,
                SocioId = usuario.SocioId
            };
        }
    }
}