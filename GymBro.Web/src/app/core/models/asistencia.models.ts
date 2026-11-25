/** Etapa 11: contratos de AsistenciaController y CuotasController. */

export interface QrAsistencia {
  token: string;
  expiraUtc: string;
  duracionSegundos: number;
}

export interface Asistencia {
  id: string;
  socioId: string;
  socioNombre: string;
  gimnasioId: string;
  gimnasioNombre: string;
  instante: string;
  fecha: string;
  metodo: 'Qr' | 'Manual';
  registradoPorUsuarioId: string;
}

export interface RegistroAsistencia {
  /** true si el socio ya tenía la asistencia del día (no se crea otra). */
  yaRegistrada: boolean;
  asistencia: Asistencia;
}

export interface AsistenciaDelDia {
  fecha: string;
  total: number;
  asistencias: Asistencia[];
}

export interface GenerarCuotas {
  anio: number;
  mes: number;
  monto: number;
  /** yyyy-MM-dd */
  vencimiento: string;
}

export interface GenerarCuotasResultado {
  creadas: number;
  existentes: number;
}

export interface Cuota {
  id: string;
  socioId: string;
  socioNombre: string;
  gimnasioId: string;
  gimnasioNombre: string;
  anio: number;
  mes: number;
  monto: number;
  vencimiento: string;
  fechaPago: string | null;
  /** Estado guardado. "Vencida" no es un estado: es el campo derivado `vencida`. */
  estado: 'Pendiente' | 'Pagada';
  vencida: boolean;
  diasVencida: number;
}
