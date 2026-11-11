using GymBro.Domain.Enums;
using GymBro.Domain.Security;

namespace GymBro.Api.Security
{
    /// <summary>Lee la identidad desde los claims del JWT del request actual.</summary>
    public class HttpUsuarioActual : IUsuarioActual
    {
        private readonly IHttpContextAccessor _accessor;
        public HttpUsuarioActual(IHttpContextAccessor accessor) { _accessor = accessor; }

        private System.Security.Claims.ClaimsPrincipal? User => _accessor.HttpContext?.User;

        public bool EsProcesoDeSistema => _accessor.HttpContext is null;
        public bool EsAutenticado => User?.Identity?.IsAuthenticated == true;

        public Guid? UsuarioId => LeerGuid(ClaimNames.Sub);
        public Guid? GimnasioId => LeerGuid(ClaimNames.GimnasioId);
        public Guid? SocioId => LeerGuid(ClaimNames.SocioId);

        public RolUsuario? Rol =>
Enum.TryParse<RolUsuario>(User?.FindFirst(ClaimNames.Role)?.Value, out var rol) ? rol : null;

        private Guid? LeerGuid(string claim) =>
Guid.TryParse(User?.FindFirst(claim)?.Value, out var id) ? id : null;
    }

    public static class ClaimNames
    {
        public const string Sub = "sub";
        public const string Role = "role";
        public const string Name = "name";
        public const string GimnasioId = "gimnasio_id";
        public const string SocioId = "socio_id";
    }
}