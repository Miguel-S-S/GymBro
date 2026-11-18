import { RutinaEjercicio } from '../models/acceso.models';
import { TipoMedida } from '../models/ejercicio.models';

// ---------- Tipo de medida (Etapa 9a-1): solo cambian la unidad y la etiqueta ----------

/** ¿El ejercicio se mide en segundos? Sin dato (respuestas o borradores anteriores) se asume repeticiones. */
export function enSegundos(tipo: TipoMedida | null | undefined): boolean {
  return tipo === 'Segundos';
}

/** Unidad corta para un valor o un rango: `s` o `reps`. */
export function unidadCorta(tipo: TipoMedida | null | undefined): string {
  return enSegundos(tipo) ? 's' : 'reps';
}

/** Etiqueta del campo que se carga: "Segundos" o "Repeticiones". */
export function etiquetaMedida(tipo: TipoMedida | null | undefined): string {
  return enSegundos(tipo) ? 'Segundos' : 'Repeticiones';
}

/** Encabezado corto de columna: "Seg." o "Reps". */
export function columnaMedida(tipo: TipoMedida | null | undefined): string {
  return enSegundos(tipo) ? 'Seg.' : 'Reps';
}

/** Rango con unidad: `8–12 reps`, `20–40 s`, `10 reps`. */
export function rangoConUnidad(min: number, max: number, tipo: TipoMedida | null | undefined): string {
  return `${min === max ? `${min}` : `${min}–${max}`} ${unidadCorta(tipo)}`;
}

/** Resumen de una línea de un ejercicio prescripto: `3 series × 8–12 reps · RIR 2 · 90 s` (o `× 20–40 s` si es por tiempo). */
export function resumenEjercicio(e: Pick<RutinaEjercicio,
  'seriesObjetivo' | 'repeticionesMinimas' | 'repeticionesMaximas' | 'rirObjetivo' | 'descansoObjetivoSegundos'>
  & { tipoMedida?: TipoMedida | null }): string {
  const rango = rangoConUnidad(e.repeticionesMinimas, e.repeticionesMaximas, e.tipoMedida);
  const rir = e.rirObjetivo === null || e.rirObjetivo === undefined ? '' : ` · RIR ${e.rirObjetivo}`;
  return `${e.seriesObjetivo} ${e.seriesObjetivo === 1 ? 'serie' : 'series'} × ${rango}${rir} · ${e.descansoObjetivoSegundos} s`;
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
