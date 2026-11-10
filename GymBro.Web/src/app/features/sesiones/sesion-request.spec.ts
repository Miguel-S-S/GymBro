import { construirRegistrarSesionRequest, SesionFormValue } from './sesion-request';

const ejercicio = {
  id: '11111111-1111-1111-1111-111111111111',
  nombre: 'Sentadilla con barra',
  zonaObjetivo: 'Piernas',
  musculoPrincipal: 'Cuádriceps',
  equipamiento: 'Barra',
  dificultad: 3,
};

function formBase(): SesionFormValue {
  return {
    inicio: '2026-11-09T18:00',
    fin: '2026-11-09T19:10',
    caloriasQuemadas: null,
    frecuenciaCardiacaMaxima: 165,
    frecuenciaCardiacaPromedio: null,
    ejercicios: [
      {
        ejercicio,
        observaciones: '  buena técnica  ',
        series: [
          { pesoLevantadoKg: 40, repeticiones: 10, rir: null, esCalentamiento: true, tiempoDescansoSegundos: 60 },
          { pesoLevantadoKg: 80, repeticiones: 8, rir: 2, esCalentamiento: false, tiempoDescansoSegundos: null },
        ],
      },
    ],
  };
}

describe('construirRegistrarSesionRequest', () => {
  it('numera ejercicios y series desde 1 según su posición', () => {
    const r = construirRegistrarSesionRequest('socio-1', formBase());
    expect(r.ejercicios[0].orden).toBe(1);
    expect(r.ejercicios[0].seriesRealizadas.map((s) => s.ordenSerie)).toEqual([1, 2]);
  });

  it('convierte las fechas locales a ISO UTC y deja la sesión sin prescripción', () => {
    const r = construirRegistrarSesionRequest('socio-1', formBase());
    expect(r.inicio).toBe(new Date('2026-11-09T18:00').toISOString());
    expect(r.fin).toBe(new Date('2026-11-09T19:10').toISOString());
    expect(r.prescripcionEntrenamientoId).toBeNull();
    expect(r.socioId).toBe('socio-1');
  });

  it('envía null en los opcionales vacíos y recorta las observaciones', () => {
    const v = formBase();
    v.fin = null;
    v.ejercicios[0].observaciones = '   ';
    const r = construirRegistrarSesionRequest('socio-1', v);
    expect(r.fin).toBeNull();
    expect(r.caloriasQuemadas).toBeNull();
    expect(r.frecuenciaCardiacaMaxima).toBe(165);
    expect(r.ejercicios[0].observaciones).toBeNull();
    expect(r.ejercicios[0].seriesRealizadas[0].rir).toBeNull();
    expect(r.ejercicios[0].seriesRealizadas[1].tiempoDescansoSegundos).toBeNull();
  });

  it('conserva la marca de calentamiento y el ejercicio elegido', () => {
    const r = construirRegistrarSesionRequest('socio-1', formBase());
    expect(r.ejercicios[0].ejercicioId).toBe(ejercicio.id);
    expect(r.ejercicios[0].seriesRealizadas[0].esCalentamiento).toBe(true);
    expect(r.ejercicios[0].seriesRealizadas[1].esCalentamiento).toBe(false);
  });

  it('falla si un ejercicio no tiene selección del catálogo', () => {
    const v = formBase();
    v.ejercicios[0].ejercicio = null;
    expect(() => construirRegistrarSesionRequest('socio-1', v)).toThrow();
  });
});
