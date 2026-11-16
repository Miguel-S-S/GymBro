/**
 * Estado de acceso de un socio (GymBro.Application.DTOs.EstadosAcceso):
 * Activo = ya tiene cuenta; Invitado = tiene una invitación vigente; SinAcceso = la invitación venció o fue reemplazada.
 */
export type EstadoAcceso = 'Activo' | 'Invitado' | 'SinAcceso';

export const ESTADO_ACCESO_ETIQUETA: Record<EstadoAcceso, string> = {
  Activo: 'Activo',
  Invitado: 'Invitación pendiente',
  SinAcceso: 'Sin acceso',
};

/** GymBro.Application.DTOs.SocioResponseDto */
export interface SocioResumen {
  id: string;
  nombreCompleto: string;
  email: string;
  fechaAlta: string;
  gimnasioId?: string | null;
  estadoAcceso: EstadoAcceso;
}

/** GymBro.Application.DTOs.SocioDetalleDto */
export interface SocioDetalle {
  id: string;
  nombre: string;
  apellido: string;
  nombreCompleto: string;
  email: string;
  /** Solo lo ven el Dueño y el propio socio. */
  dni?: string | null;
  estadoAcceso: EstadoAcceso;
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
  /** Obligatorio: identifica al socio y habilita la validación cruzada de vinculación. */
  dni: string;
}

/** GymBro.Application.DTOs.SocioUpdateDto: corrección de la ficha mientras el socio no activó su cuenta. */
export type SocioUpdate = SocioCreate;

/** GymBro.Application.DTOs.InvitacionResponseDto: el código se muestra una sola vez. */
export interface Invitacion {
  token: string;
  expiraUtc: string;
}

/** GymBro.Application.DTOs.SocioAltaResponseDto */
export interface SocioAlta {
  socio: SocioResumen;
  invitacion: Invitacion;
}

/** Enlace que se le entrega al socio para activar su cuenta (ruta pública /activar/:token). */
export function enlaceInvitacion(token: string, origen: string): string {
  return `${origen.replace(/\/+$/, '')}/activar/${encodeURIComponent(token)}`;
}

export const OBJETIVO_ETIQUETA: Record<NonNullable<SocioDetalle['objetivo']>, string> = {
  PerdidaPeso: 'Pérdida de peso',
  GananciaMuscular: 'Ganancia muscular',
  Mantenimiento: 'Mantenimiento',
};