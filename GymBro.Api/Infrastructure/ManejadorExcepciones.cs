using GymBro.Application.Services;
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
                // Etapa 9b-1: GymBro.IA u Ollama no disponibles; mensaje propio, sin detalle interno.
                ServicioExternoNoDisponibleException => (StatusCodes.Status503ServiceUnavailable, "Servicio no disponible"),
                // Etapa 10a: problema de datos o configuración del sistema; el mensaje es propio (sin detalle interno).
                CatalogosIncompletosException => (StatusCodes.Status500InternalServerError, "Catálogo incompleto"),
                _ => (StatusCodes.Status500InternalServerError, "Error interno")
            };

            if (ex is ServicioExternoNoDisponibleException)
            {
                _logger.LogWarning(ex, "Servicio externo no disponible");
            }
            else if (ex is CatalogosIncompletosException)
            {
                _logger.LogError(ex, "Catalogo incompleto");
            }
            else if (status == StatusCodes.Status500InternalServerError)
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