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
  /** Etapa 9a-2: gimnasio dueño del ejercicio (null en los globales). */
  gimnasioId?: string | null;
  tipoEquipamientoId?: string | null;
  /** Nombre del tipo de equipamiento normalizado. */
  tipoEquipamiento?: string | null;
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
  /** Etapa 9a-2. */
  origen?: OrigenEjercicio | null;
  pagina: number;
  tamanioPagina: number;
}

/** Etiqueta en español del origen (insignia del catálogo, Etapa 9a-2). */
export const ORIGEN_ETIQUETA: Record<OrigenEjercicio, string> = {
  Dataset: 'Dataset',
  Proyecto: 'Proyecto',
  Gimnasio: 'Gimnasio',
};

/** GymBro.Application.DTOs.EjercicioGimnasioRequest (alta y edición de un ejercicio propio, Etapa 9a-2). */
export interface EjercicioGimnasioRequest {
  nombre: string;
  zonaObjetivo: string;
  tipoEquipamientoId: string;
  musculoPrincipal: string;
  sinergistas?: string | null;
  estabilizadores?: string | null;
  preparacion?: string | null;
  ejecucion: string;
  tipoMedida: TipoMedida;
  dificultad: number;
}

/** GymBro.Application.DTOs.EquipamientoItemDto */
export interface EquipamientoItem {
  tipoEquipamientoId: string;
  clave: string;
  nombre: string;
  siempreDisponible: boolean;
  propio: boolean;
  marcado: boolean;
  cantidad: number | null;
}

/** GymBro.Application.DTOs.EquipamientoGimnasioDto */
export interface EquipamientoGimnasio {
  gimnasioId: string;
  nombreGimnasio: string;
  /** Solo el Dueño del gimnasio puede modificarlo. */
  editable: boolean;
  items: EquipamientoItem[];
}

/** Límites del servidor (EjercicioGimnasioService / TextoValidado). */
export const LIMITES_EJERCICIO = { nombre: 120, textoLibre: 2000, dificultadMin: 1, dificultadMax: 5 } as const;

/** Tipos que se pueden elegir para un ejercicio propio: los siempre disponibles y los marcados. */
export function tiposElegibles(e: EquipamientoGimnasio): EquipamientoItem[] {
  return e.items.filter((i) => i.siempreDisponible || i.marcado);
}
