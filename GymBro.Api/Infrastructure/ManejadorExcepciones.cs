using GymBro.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace GymBro.Api.Infrastructure
{
    /// <summary>Traduce las excepciones de negocio a respuestas HTTP coherentes (ProblemDetails).</summary>
    public class ManejadorExcepciones : IExceptionHandler
    {
        private readonly ILogger<ManejadorExcepciones> _logger;
        public ManejadorExcepciones(ILogger<ManejadorExcepciones> logger) { _logger = logger; }

        public async ValueTask<bool> TryHandleAsync(HttpContext contexto, Exception ex, CancellationToken ct)
        {
            var (status, titulo) = ex switch
            {
                ReglaNegocioException or ArgumentException => (StatusCodes.Status400BadRequest, "Solicitud inválida"),
                NoAutenticadoException => (StatusCodes.Status401Unauthorized, "No autenticado"),
                AccesoDenegadoException => (StatusCodes.Status403Forbidden, "Acceso denegado"),
                RecursoNoEncontradoException => (StatusCodes.Status404NotFound, "No encontrado"),
                _ => (StatusCodes.Status500InternalServerError, "Error interno")
            };

            if (status == StatusCodes.Status500InternalServerError)
            {
                _logger.LogError(ex, "Error no controlado");
                // No se expone el detalle interno al cliente.
                ex = new Exception("Ocurrió un error inesperado.");
            }

            contexto.Response.StatusCode = status;
            await contexto.Response.WriteAsJsonAsync(
                new ProblemDetails { Status = status, Title = titulo, Detail = ex.Message }, ct);
            return true;
        }
    }
}