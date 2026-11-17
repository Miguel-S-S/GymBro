import { RutinaEjercicio } from '../models/acceso.models';

/** Resumen de una línea de un ejercicio prescripto: `3 series × 8–12 reps · RIR 2 · 90 s`. */
export function resumenEjercicio(e: Pick<RutinaEjercicio,
  'seriesObjetivo' | 'repeticionesMinimas' | 'repeticionesMaximas' | 'rirObjetivo' | 'descansoObjetivoSegundos'>): string {
  const reps = e.repeticionesMinimas === e.repeticionesMaximas
    ? `${e.repeticionesMinimas}`
    : `${e.repeticionesMinimas}–${e.repeticionesMaximas}`;
  const rir = e.rirObjetivo === null || e.rirObjetivo === undefined ? '' : ` · RIR ${e.rirObjetivo}`;
  return `${e.seriesObjetivo} ${e.seriesObjetivo === 1 ? 'serie' : 'series'} × ${reps} reps${rir} · ${e.descansoObjetivoSegundos} s`;
}

/** Etiqueta legible del estado de una prescripción (valores de `EstadoPrescripcion`). */
export function etiquetaEstado(estado: string): string {
  switch (estado) {
    case 'PendienteRevision': return 'Pendiente de revisión';
    case 'Aprobada': return 'Aprobada';
    case 'Editada': return 'Editada por el entrenador';
    case 'Rechazada': return 'Rechazada';
    default: return estado;
  }
}

/** Campos numéricos que se editan con los botones +/− de la tarjeta de ejercicio. */
export type CampoRutina = 'seriesObjetivo' | 'repeticionesMinimas' | 'repeticionesMaximas' | 'rirObjetivo' | 'descansoObjetivoSegundos';

/**
 * Aplica un paso (+/−) a un campo respetando las mismas reglas que valida la API
 * (series ≥ 1; reps mínimas ≥ 1 y ≤ máximas; RIR 0–5; descanso > 0).
 * Devuelve el ejercicio con el valor nuevo; si el paso violaría una regla, lo devuelve sin cambios.
 */
export function aplicarPaso(e: RutinaEjercicio, campo: CampoRutina, paso: number): RutinaEjercicio {
  const actual = campo === 'rirObjetivo' ? (e.rirObjetivo ?? 0) : e[campo];
  const nuevo = actual + paso;
  const copia = { ...e, [campo]: nuevo } as RutinaEjercicio;
  switch (campo) {
    case 'seriesObjetivo': if (nuevo < 1) return e; break;
    case 'repeticionesMinimas': if (nuevo < 1 || nuevo > e.repeticionesMaximas) return e; break;
    case 'repeticionesMaximas': if (nuevo < e.repeticionesMinimas) return e; break;
    case 'rirObjetivo': if (nuevo < 0 || nuevo > 5) return e; break;
    case 'descansoObjetivoSegundos': if (nuevo < 15) return e; break;
  }
  return copia;
}
