/**
 * Temporizador de descanso basado en la hora de fin (`finEpoch`), no en un contador por segundo:
 * si el navegador frena los intervalos con la pestaña en segundo plano, al volver el tiempo restante
 * sigue siendo correcto porque se recalcula con el reloj.
 */
export interface EstadoTemporizador {
  /** Momento de fin, en milisegundos desde epoch. */
  finEpoch: number;
  /** Duración original (para mostrar el progreso). */
  duracionSegundos: number;
}

export function iniciarDescanso(duracionSegundos: number, ahora: number = Date.now()): EstadoTemporizador {
  const duracion = Math.max(0, Math.round(duracionSegundos));
  return { finEpoch: ahora + duracion * 1000, duracionSegundos: duracion };
}

/** Segundos que faltan (redondeo hacia arriba: muestra 1 hasta que realmente termina). Nunca negativo. */
export function segundosRestantes(t: EstadoTemporizador | null, ahora: number = Date.now()): number {
  if (!t) return 0;
  return Math.max(0, Math.ceil((t.finEpoch - ahora) / 1000));
}

export function terminado(t: EstadoTemporizador | null, ahora: number = Date.now()): boolean {
  return !t || ahora >= t.finEpoch;
}

/** 0–1: fracción transcurrida (para una barra de progreso). */
export function progreso(t: EstadoTemporizador | null, ahora: number = Date.now()): number {
  if (!t || t.duracionSegundos === 0) return 1;
  const total = t.duracionSegundos * 1000;
  return Math.min(1, Math.max(0, 1 - (t.finEpoch - ahora) / total));
}

/** 85 -> "01:25". */
export function formatoMmSs(segundos: number): string {
  const s = Math.max(0, Math.floor(segundos));
  return `${String(Math.floor(s / 60)).padStart(2, '0')}:${String(s % 60).padStart(2, '0')}`;
}
