/**
 * Unidad de medida del ejercicio (Etapa 9a-1). Con 'Segundos', los campos de "repeticiones" de la prescripción,
 * la dosificación y la serie guardan segundos: el cliente solo cambia la unidad y la etiqueta.
 */
export type TipoMedida = 'Repeticiones' | 'Segundos';

/** Procedencia del ejercicio (Etapa 9a-1). */
export type OrigenEjercicio = 'Dataset' | 'Proyecto' | 'Gimnasio';

/** GymBro.Application.DTOs.EjercicioResumenDto */
export interface EjercicioResumen {
  id: string;
  nombre: string;
  zonaObjetivo: string;
  musculoPrincipal: string;
  equipamiento: string;
  dificultad: number;
  /** Etapa 9a-1. Opcional para tolerar respuestas anteriores (se asume 'Repeticiones'). */
  tipoMedida?: TipoMedida;
  origen?: OrigenEjercicio;
}

/** GymBro.Application.DTOs.EjercicioDetalleDto */
export interface EjercicioDetalle extends EjercicioResumen {
  sinergistas: string;
  estabilizadores: string;
  preparacion: string;
  ejecucion: string;
}

/** GymBro.Application.DTOs.EjercicioBuscadoDto (búsqueda semántica) */
export interface EjercicioBuscado {
  id: string;
  nombre: string;
  zonaObjetivo: string;
  musculoPrincipal: string;
  equipamiento: string;
}

/** GymBro.Application.DTOs.PaginadoDto<T> */
export interface Paginado<T> {
  items: T[];
  total: number;
  pagina: number;
  tamanioPagina: number;
}

export interface FiltroCatalogo {
  texto?: string | null;
  zona?: string | null;
  pagina: number;
  tamanioPagina: number;
}
