/** GymBro.Application.DTOs.SocioResponseDto */
export interface SocioResumen {
  id: string;
  nombreCompleto: string;
  email: string;
  fechaAlta: string;
  gimnasioId?: string | null;
}

/** GymBro.Application.DTOs.SocioDetalleDto */
export interface SocioDetalle {
  id: string;
  nombre: string;
  apellido: string;
  nombreCompleto: string;
  email: string;
  genero: string;
  fechaNacimiento: string | null;
  objetivo: 'PerdidaPeso' | 'GananciaMuscular' | 'Mantenimiento' | null;
  nivel: 'Principiante' | 'Intermedio' | 'Avanzado' | null;
  fechaAlta: string;
}

/** GymBro.Application.DTOs.SocioCreateDto */
export interface SocioCreate {
  nombre: string;
  apellido: string;
  email: string;
  /** Opcional: necesario para que el socio pueda vincularse al gimnasio por DNI. */
  dni?: string;
}

export const OBJETIVO_ETIQUETA: Record<NonNullable<SocioDetalle['objetivo']>, string> = {
  PerdidaPeso: 'Pérdida de peso',
  GananciaMuscular: 'Ganancia muscular',
  Mantenimiento: 'Mantenimiento',
};