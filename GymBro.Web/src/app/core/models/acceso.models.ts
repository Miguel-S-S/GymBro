/** GymBro.Application.DTOs.Acceso.* */
export interface Gimnasio {
  id: string;
  nombre: string;
  codigo: string;
  requiereAprobacionRutinas: boolean;
  activo: boolean;
  fechaAlta: string;
}

export interface GimnasioCreate {
  nombre: string;
  duenoNombreCompleto: string;
  duenoEmail: string;
  duenoPassword: string;
  requiereAprobacionRutinas: boolean;
}

export interface Entrenador {
  id: string;
  nombreCompleto: string;
  email: string;
  activo: boolean;
}

export interface EntrenadorCreate {
  nombreCompleto: string;
  email: string;
  password: string;
}

export type EstadoSolicitud = 'Pendiente' | 'Aprobada' | 'Rechazada';

export interface SolicitudVinculacion {
  id: string;
  socioId: string;
  nombreSocio: string;
  dni: string;
  gimnasioId: string;
  estado: EstadoSolicitud;
  fechaAlta: string;
  motivo: string | null;
}

export interface RutinaEjercicio {
  id: string;
  orden: number;
  ejercicio: string;
  seriesObjetivo: number;
  repeticionesMinimas: number;
  repeticionesMaximas: number;
  rirObjetivo: number | null;
  descansoObjetivoSegundos: number;
  observaciones: string | null;
}

export interface RutinaPendiente {
  id: string;
  socioId: string;
  nombreSocio: string;
  origenGeneracion: string;
  observaciones: string | null;
  estado: string;
  fechaAlta: string;
  ejercicios: RutinaEjercicio[];
}

export type DecisionRevision = 'Aprobada' | 'Editada' | 'Rechazada';

export interface AjusteEjercicio {
  prescripcionEjercicioId: string;
  seriesObjetivo?: number;
  repeticionesMinimas?: number;
  repeticionesMaximas?: number;
  rirObjetivo?: number;
  descansoObjetivoSegundos?: number;
  observaciones?: string;
}

export interface RevisarRutina {
  decision: DecisionRevision;
  comentario?: string;
  ajustes: AjusteEjercicio[];
}

export interface AuditLog {
  id: string;
  fecha: string;
  usuarioId: string | null;
  rol: string | null;
  gimnasioId: string | null;
  accion: string;
  entidad: string;
  entidadId: string | null;
  detalle: string | null;
}