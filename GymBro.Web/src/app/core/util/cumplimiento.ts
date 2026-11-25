import { EjercicioComparado } from '../models/sesion.models';
import { enSegundos } from './rutina-formato';

/** Etapa 8d: indicadores de cumplimiento de un ejercicio, sin veredicto global. */
export interface IndicadoresCumplimiento {
  /** Series efectivas registradas (sin calentamiento). Más series que el objetivo no es incumplimiento. */
  series: number;
  /** Series efectivas con minimo <= valor <= maximo (extremos incluidos). */
  enRango: number;
  /** "Repeticiones" o "Segundos", segun el tipo de medida del ejercicio (Etapa 9a-1). */
  etiquetaRango: string;
}

/**
 * Etapa 8d: calcula "Series: N de M" y "Repeticiones en rango: K de N series" con los datos que ya devuelve
 * GET /api/Sesiones/{id}/comparacion (sin cambiar el contrato). El RIR no participa.
 * Etapa 8e: se movio desde features/mi/mi-historial.ts para que la use tambien la vista del Dueno y del Personal
 * Trainer (features/sesiones/sesion-detalle.ts). Misma logica, sin cambios de comportamiento.
 */
export function indicadoresCumplimiento(e: EjercicioComparado): IndicadoresCumplimiento {
  const efectivas = (e.ejecucion?.detallesSeries ?? []).filter((s) => !s.esCalentamiento);
  const p = e.prescripcion;
  const enRango = p
    ? efectivas.filter((s) => s.repeticiones >= p.repeticionesMinimas && s.repeticiones <= p.repeticionesMaximas).length
    : 0;
  return { series: efectivas.length, enRango, etiquetaRango: enSegundos(e.tipoMedida) ? 'Segundos' : 'Repeticiones' };
}
