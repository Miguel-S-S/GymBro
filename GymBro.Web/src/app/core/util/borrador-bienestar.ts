import { PREFIJO_BORRADOR_BIENESTAR } from './borrador-sesion';

/**
 * Borrador local del registro de bienestar (Etapa 8b), como el de la sesión en curso: sin conexión no se pierde
 * lo cargado, pero el envío requiere red (no hay cola de sincronización). Se guarda con su fecha para no aplicar
 * el de ayer al día siguiente. Se borra al guardar en el servidor y al cerrar sesión (borrarTodosLosBorradores).
 */
export interface BorradorBienestar<T> {
  /** Día al que corresponde (yyyy-MM-dd). */
  fecha: string;
  formulario: T;
}

type Almacen = Pick<Storage, 'getItem' | 'setItem' | 'removeItem'>;

function almacenPorDefecto(): Almacen | null {
  try {
    return globalThis.localStorage ?? null;
  } catch {
    return null;
  }
}

export function guardarBorradorBienestar<T>(socioId: string, borrador: BorradorBienestar<T>, almacen = almacenPorDefecto()): void {
  try {
    almacen?.setItem(PREFIJO_BORRADOR_BIENESTAR + socioId, JSON.stringify(borrador));
  } catch {
    /* sin almacenamiento: vive solo en memoria */
  }
}

/** Devuelve el borrador del socio; si se indica `fecha`, solo cuando corresponde a ese día. */
export function leerBorradorBienestar<T>(socioId: string, fecha?: string, almacen = almacenPorDefecto()): BorradorBienestar<T> | null {
  try {
    const crudo = almacen?.getItem(PREFIJO_BORRADOR_BIENESTAR + socioId);
    if (!crudo) return null;
    const b = JSON.parse(crudo) as BorradorBienestar<T>;
    if (!b || typeof b.fecha !== 'string' || !b.formulario) return null;
    return fecha && b.fecha !== fecha ? null : b;
  } catch {
    return null; // almacenamiento bloqueado o contenido corrupto
  }
}

export function borrarBorradorBienestar(socioId: string, almacen = almacenPorDefecto()): void {
  try {
    almacen?.removeItem(PREFIJO_BORRADOR_BIENESTAR + socioId);
  } catch {
    /* nada que borrar */
  }
}
