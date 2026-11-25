import { TipoMedida } from './ejercicio.models';

// ---- Request: GymBro.Application.DTOs.RegistrarSesionEntrenamientoRequest ----

export interface SerieRealizadaRequest {
  ordenSerie: number;
  pesoLevantadoKg: number;
  repeticiones: number;
  rir: number | null;
  esCalentamiento: boolean;
  tiempoDescansoSegundos: number | null;
}

export interface SesionEjercicioRequest {
  ejercicioId: string;
  orden: number;
  observaciones: string | null;
  seriesRealizadas: SerieRealizadaRequest[];
}

export interface RegistrarSesionRequest {
  socioId: string;
  prescripcionEntrenamientoId: string | null;
  inicio: string;
  fin: string | null;
  caloriasQuemadas: number | null;
  frecuenciaCardiacaMaxima: number | null;
  frecuenciaCardiacaPromedio: number | null;
  ejercicios: SesionEjercicioRequest[];
}

// ---- Response: GymBro.Application.DTOs.SesionEntrenamientoResponseDto ----

export interface SerieRealizada extends SerieRealizadaRequest {
  id: string;
}

export interface SesionEjercicio {
  id: string;
  ejercicioId: string;
  orden: number;
  observaciones: string | null;
  seriesRealizadas: SerieRealizada[];
}

export interface Sesion {
  id: string;
  socioId: string;
  prescripcionEntrenamientoId: string | null;
  inicio: string;
  fin: string | null;
  caloriasQuemadas: number;
  frecuenciaCardiacaMaxima: number;
  frecuenciaCardiacaPromedio: number;
  ejercicios: SesionEjercicio[];
}

// ---- Response: GymBro.Application.DTOs.Comparacion.ComparacionSesionResponseDto ----

export interface ComparacionMetrica {
  cumpleObjetivo: boolean;
  desviacion: number;
  mensaje: string;
}

export interface SerieComparada {
  ordenSerie: number;
  pesoLevantadoKg: number;
  repeticiones: number;
  rir: number | null;
  esCalentamiento: boolean;
  tiempoDescansoSegundos: number | null;
  volumen: number;
  repeticionesComparacion: ComparacionMetrica | null;
  rirComparacion: ComparacionMetrica | null;
}

export interface PrescripcionResumen {
  seriesObjetivo: number;
  repeticionesMinimas: number;
  repeticionesMaximas: number;
  rirObjetivo: number | null;
  descansoObjetivoSegundos: number;
}

export interface EjecucionResumen {
  cantidadSeriesTotales: number;
  cantidadSeriesEfectivas: number;
  cantidadSeriesCalentamiento: number;
  volumenTotalKg: number;
  volumenEfectivoKg: number;
  volumenCalentamientoKg: number;
  detallesSeries: SerieComparada[];
}

export interface EjercicioComparado {
  ejercicioId: string;
  orden: number;
  /** Etapa 9a-1: tomado del ejercicio. Con 'Segundos', "repeticiones" son segundos y el volumen en kg es 0. */
  tipoMedida?: TipoMedida;
  prescripcion: PrescripcionResumen | null;
  ejecucion: EjecucionResumen | null;
  comparacion: {
    series: {
      prescritas: number;
      realizadasEfectivas: number;
      diferencia: number;
      /**
       * @deprecated Etapa 8d: veredicto "Cumple objetivo" / "Faltaron N" / "Excedieron N". La interfaz del Socio ya no
       * lo muestra (usa indicadoresCumplimiento). Etapa 8e: la vista del Dueño y del Personal Trainer
       * (sesiones/sesion-detalle.ts) tampoco. Ninguna pantalla lo lee; se conserva en el contrato (sin cambios de API).
       */
      mensaje: string;
    };
  };
}

export interface ComparacionSesion {
  sesionId: string;
  prescripcionEntrenamientoId: string | null;
  ejercicios: EjercicioComparado[];
}
