import { formatoMmSs, iniciarDescanso, progreso, segundosRestantes, terminado } from './temporizador';

const T0 = Date.UTC(2026, 10, 16, 18, 0, 0); // reloj simulado

describe('temporizador de descanso', () => {
  it('calcula el restante con la hora de fin', () => {
    const t = iniciarDescanso(90, T0);
    expect(segundosRestantes(t, T0)).toBe(90);
    expect(segundosRestantes(t, T0 + 30_000)).toBe(60);
  });

  it('no se desfasa si no hubo ticks (pestaña en segundo plano)', () => {
    const t = iniciarDescanso(90, T0);
    // Sin ningún tick intermedio: el navegador congeló los intervalos 75 s.
    expect(segundosRestantes(t, T0 + 75_000)).toBe(15);
    expect(terminado(t, T0 + 75_000)).toBe(false);
    // Volvió después del fin: terminado y en 0, nunca negativo.
    expect(segundosRestantes(t, T0 + 200_000)).toBe(0);
    expect(terminado(t, T0 + 200_000)).toBe(true);
  });

  it('redondea hacia arriba hasta el final exacto', () => {
    const t = iniciarDescanso(10, T0);
    expect(segundosRestantes(t, T0 + 9_001)).toBe(1);
    expect(segundosRestantes(t, T0 + 10_000)).toBe(0);
    expect(terminado(t, T0 + 9_999)).toBe(false);
    expect(terminado(t, T0 + 10_000)).toBe(true);
  });

  it('sin temporizador: restante 0 y terminado', () => {
    expect(segundosRestantes(null, T0)).toBe(0);
    expect(terminado(null, T0)).toBe(true);
  });

  it('informa el progreso entre 0 y 1', () => {
    const t = iniciarDescanso(100, T0);
    expect(progreso(t, T0)).toBe(0);
    expect(progreso(t, T0 + 50_000)).toBeCloseTo(0.5);
    expect(progreso(t, T0 + 500_000)).toBe(1);
  });

  it('formatea mm:ss', () => {
    expect(formatoMmSs(85)).toBe('01:25');
    expect(formatoMmSs(0)).toBe('00:00');
    expect(formatoMmSs(600)).toBe('10:00');
  });
});
