/**
 * Borrador de la sesión en curso en el almacenamiento local del navegador (decisión de la Etapa 8):
 * un recargo accidental no pierde lo cargado. Se borra al finalizar la sesión y al cerrar sesión.
 * No hay cola de sincronización sin conexión (trabajo futuro).
 * Si el almacenamiento no está disponible (modo privado, bloqueado), se trabaja solo en memoria.
 */
export const PREFIJO_BORRADOR = 'gymbro.borrador-sesion.';

type Almacen = Pick<Storage, 'getItem' | 'setItem' | 'removeItem' | 'key' | 'length'>;

function almacenPorDefecto(): Almacen | null {
  try {
    return globalThis.localStorage ?? null;
  } catch {
    return null;
  }
}

export function guardarBorrador<T>(socioId: string, borrador: T, almacen: Almacen | null = almacenPorDefecto()): void {
  try {
    almacen?.setItem(PREFIJO_BORRADOR + socioId, JSON.stringify(borrador));
  } catch {
    /* sin almacenamiento: el borrador vive solo en memoria */
  }
}

export function leerBorrador<T>(socioId: string, almacen: Almacen | null = almacenPorDefecto()): T | null {
  try {
    const crudo = almacen?.getItem(PREFIJO_BORRADOR + socioId);
    return crudo ? (JSON.parse(crudo) as T) : null;
  } catch {
    return null; // almacenamiento bloqueado o contenido corrupto
  }
}

export function borrarBorrador(socioId: string, almacen: Almacen | null = almacenPorDefecto()): void {
  try {
    almacen?.removeItem(PREFIJO_BORRADOR + socioId);
  } catch {
    /* nada que borrar */
  }
}

/** Al cerrar sesión: elimina todos los borradores del dispositivo (pueden contener datos de entrenamiento). */
export function borrarTodosLosBorradores(almacen: Almacen | null = almacenPorDefecto()): void {
  try {
    if (!almacen) return;
    const claves: string[] = [];
    for (let i = 0; i < almacen.length; i++) {
      const k = almacen.key(i);
      if (k?.startsWith(PREFIJO_BORRADOR)) claves.push(k);
    }
    claves.forEach((k) => almacen.removeItem(k));
  } catch {
    /* nada que borrar */
  }
}
