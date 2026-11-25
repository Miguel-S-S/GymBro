namespace GymBro.Domain.Exceptions
{
    /// <summary>Violación de una regla de negocio (HTTP 400).</summary>
    public class ReglaNegocioException : Exception
    {
        public ReglaNegocioException(string mensaje) : base(mensaje) { }
    }

    /// <summary>El usuario autenticado no tiene permiso sobre el recurso (HTTP 403).</summary>
    public class AccesoDenegadoException : Exception
    {
        public AccesoDenegadoException(string mensaje = "No tiene permisos para acceder a este recurso.") : base(mensaje) { }
    }

    /// <summary>Credenciales inválidas o usuario inactivo (HTTP 401).</summary>
    public class NoAutenticadoException : Exception
    {
        public NoAutenticadoException(string mensaje = "Credenciales inválidas.") : base(mensaje) { }
    }

    /// <summary>El recurso no existe o no es visible para el tenant actual (HTTP 404).</summary>
    public class RecursoNoEncontradoException : Exception
    {
        public RecursoNoEncontradoException(string mensaje) : base(mensaje) { }
    }

    /// <summary>
    /// Etapa 9b-1: un servicio externo (GymBro.IA u Ollama) no respondió o respondió con error (HTTP 503). El mensaje es
    /// propio y no incluye el detalle técnico; la operación no produce efectos.
    /// </summary>
    public class ServicioExternoNoDisponibleException : Exception
    {
        public ServicioExternoNoDisponibleException(string mensaje, Exception? causa = null) : base(mensaje, causa) { }
    }
}