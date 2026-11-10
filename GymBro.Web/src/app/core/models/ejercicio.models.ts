/** GymBro.Application.DTOs.EjercicioResumenDto */
export interface EjercicioResumen {
  id: string;
  nombre: string;
  zonaObjetivo: string;
  musculoPrincipal: string;
  equipamiento: string;
  dificultad: number;
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
