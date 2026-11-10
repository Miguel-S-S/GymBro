import { HttpErrorResponse } from '@angular/common/http';
import { mensajeDeError } from './api-error';
import { duracionLegible, oSinDato } from './formato';

describe('mensajeDeError', () => {
  it('usa el campo error de { error: "..." }', () => {
    const e = new HttpErrorResponse({ status: 400, error: { error: 'El RIR debe estar entre 0 y 5.' } });
    expect(mensajeDeError(e)).toBe('El RIR debe estar entre 0 y 5.');
  });

  it('usa el texto plano que devuelve BadRequest(string)', () => {
    const e = new HttpErrorResponse({ status: 400, error: 'Debe especificar un socioId válido.' });
    expect(mensajeDeError(e)).toBe('Debe especificar un socioId válido.');
  });

  it('junta los mensajes de ValidationProblemDetails', () => {
    const e = new HttpErrorResponse({
      status: 400,
      error: { title: 'One or more validation errors occurred.', errors: { Email: ['Requerido.'], Nombre: ['Largo.'] } },
    });
    expect(mensajeDeError(e)).toBe('Requerido. Largo.');
  });

  it('informa cuando no hay conexión con la API', () => {
    const e = new HttpErrorResponse({ status: 0 });
    expect(mensajeDeError(e)).toContain('No se pudo conectar');
  });
});

describe('duracionLegible', () => {
  it('formatea horas y minutos', () => {
    expect(duracionLegible('2026-11-09T18:00:00Z', '2026-11-09T19:05:00Z')).toBe('1 h 05 min');
    expect(duracionLegible('2026-11-09T18:00:00Z', '2026-11-09T18:45:00Z')).toBe('45 min');
  });

  it('devuelve null sin fin o con fin anterior al inicio', () => {
    expect(duracionLegible('2026-11-09T18:00:00Z', null)).toBeNull();
    expect(duracionLegible('2026-11-09T18:00:00Z', '2026-11-09T17:00:00Z')).toBeNull();
  });
});

describe('oSinDato', () => {
  it('trata 0, null y undefined como sin dato', () => {
    expect(oSinDato(0)).toBeNull();
    expect(oSinDato(null)).toBeNull();
    expect(oSinDato(undefined)).toBeNull();
    expect(oSinDato(72)).toBe(72);
  });
});
