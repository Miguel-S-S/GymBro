/** Etapa 10a: GymBro.Application.DTOs.AlertaSeguridadDto (dato de salud). */
export type ReglaSeguridad = 'R-RIR-1' | 'R-PROG-1' | 'R-FC-1';

export interface AlertaSeguridad {
  id: string;
  socioId: string;
  nombreSocio: string;
  reglaCodigo: ReglaSeguridad;
  tipo: 'Alerta' | 'Sugerencia';
  severidad: 'Informativa' | 'Advertencia';
  /** Texto simple para el socio. */
  mensaje: string;
  estado: 'Pendiente' | 'Vista';
  fechaAlta: string;
  vistaEn: string | null;
  sesionId: string | null;
  sesionEjercicioId: string | null;
  registroBienestarId: string | null;
  ejercicio: string | null;
  /** Solo R-PROG-1. */
  cargaSugeridaKg: number | null;
  /** Entradas, umbrales, resultado y versión de las reglas. */
  detalle: Record<string, unknown>;
}

/** Etapa 10a: GymBro.Application.DTOs.ViolacionLimiteDto (R-LIM). */
export interface ViolacionLimite {
  codigo: string;
  mensaje: string;
  /** Orden del ejercicio; null si es de la rutina completa. */
  orden: number | null;
}

/** Nombre de cada regla para el personal (en la PWA del socio se muestra solo el mensaje). */
export const REGLA_ETIQUETA: Record<ReglaSeguridad, string> = {
  'R-RIR-1': 'Esfuerzo mayor al planificado',
  'R-PROG-1': 'Sugerencia de carga',
  'R-FC-1': 'FC de reposo elevada',
};

/** Valores clave del detalle, para mostrarlos al personal en una línea. */
export function resumenDetalle(a: AlertaSeguridad): string {
  const r = (a.detalle?.['resultado'] ?? {}) as Record<string, unknown>;
  const e = (a.detalle?.['entradas'] ?? {}) as Record<string, unknown>;
  switch (a.reglaCodigo) {
    case 'R-RIR-1':
      return `RIR objetivo ${e['rir_objetivo']} · mediana realizada ${r['mediana_rir']} · series con dato ${r['series_con_dato']}`;
    case 'R-PROG-1':
      return `${r['carga_actual_kg']} kg → ${r['carga_sugerida_kg']} kg (${r['cambio_porcentaje']} %)`;
    case 'R-FC-1':
      return `FC de reposo ${e['fc_reposo_lpm']} lpm · línea base ${r['linea_base_lpm']} lpm (${r['variacion_porcentaje']} %)`;
  }
}
