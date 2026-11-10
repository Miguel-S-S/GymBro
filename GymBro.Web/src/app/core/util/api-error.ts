import { HttpErrorResponse } from '@angular/common/http';

/**
 * Traduce las respuestas de error de GymBro.Api a un texto para mostrar.
 * Formatos que devuelve hoy la API:
 *  - `{ error: "..." }`           (SesionesController / EjerciciosController ante ArgumentException)
 *  - `"texto plano"`              (BadRequest/NotFound con string)
 *  - ValidationProblemDetails     (validación automática de [ApiController])
 */
export function mensajeDeError(err: unknown, porDefecto = 'Ocurrió un error inesperado.'): string {
  if (!(err instanceof HttpErrorResponse)) return porDefecto;

  if (err.status === 0) {
    return 'No se pudo conectar con GymBro.Api. Verificá que esté corriendo en http://localhost:5069.';
  }

  const cuerpo = err.error;
  if (typeof cuerpo === 'string' && cuerpo.trim()) return cuerpo;

  if (cuerpo && typeof cuerpo === 'object') {
    if (typeof cuerpo.error === 'string') return cuerpo.error;
    if (cuerpo.errors && typeof cuerpo.errors === 'object') {
      const mensajes = Object.values(cuerpo.errors as Record<string, string[]>).flat();
      if (mensajes.length) return mensajes.join(' ');
    }
    if (typeof cuerpo.title === 'string') return cuerpo.title;
  }

  if (err.status === 404) return 'No se encontró el recurso solicitado.';
  if (err.status >= 500) return 'Error interno de GymBro.Api.';
  return porDefecto;
}
