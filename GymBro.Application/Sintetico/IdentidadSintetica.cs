using GymBro.Domain.Enums;
using GymBro.Domain.Security;

namespace GymBro.Application.Sintetico
{
    /// <summary>
    /// Etapa 7e: identidad que asume el generador sintetico al invocar los servicios reales (Dueno para generar rutinas y
    /// registrar sesiones, Socio para su bienestar). Solo se registra en el modo generador (Program.cs, entorno Development
    /// y opcion explicita) o en las pruebas; nunca atiende pedidos HTTP. Es un singleton mutable: el generador es secuencial.
    /// </summary>
    public sealed class ContextoSintetico
    {
        public RolUsuario? Rol { get; private set; }
        public Guid? UsuarioId { get; private set; }
        public Guid? GimnasioId { get; private set; }
        public Guid? SocioId { get; private set; }
        public bool EsSistema { get; private set; } = true;

        public void ComoSistema() { EsSistema = true; Rol = null; UsuarioId = null; GimnasioId = null; SocioId = null; }

        public void ComoPersonal(RolUsuario rol, Guid usuarioId, Guid gimnasioId)
        {
            EsSistema = false; Rol = rol; UsuarioId = usuarioId; GimnasioId = gimnasioId; SocioId = null;
        }

        public void ComoSocio(Guid usuarioId, Guid socioId)
        {
            EsSistema = false; Rol = RolUsuario.Socio; UsuarioId = usuarioId; GimnasioId = null; SocioId = socioId;
        }
    }

    /// <summary>IUsuarioActual del modo generador: lee el ContextoSintetico.</summary>
    public sealed class UsuarioActualSintetico : IUsuarioActual
    {
        private readonly ContextoSintetico _c;
        public UsuarioActualSintetico(ContextoSintetico c) { _c = c; }

        public bool EsAutenticado => !_c.EsSistema;
        public bool EsProcesoDeSistema => _c.EsSistema;
        public Guid? UsuarioId => _c.UsuarioId;
        public RolUsuario? Rol => _c.Rol;
        public Guid? GimnasioId => _c.GimnasioId;
        public Guid? SocioId => _c.SocioId;
    }

    /// <summary>Etapa 7e: reloj simulado del generador (los servicios calculan "hoy" con TimeProvider).</summary>
    public sealed class RelojSintetico : TimeProvider
    {
        private DateTimeOffset _ahora = DateTimeOffset.UnixEpoch;
        public void Fijar(DateTimeOffset instante) => _ahora = instante.ToUniversalTime();
        public override DateTimeOffset GetUtcNow() => _ahora;
    }
}
