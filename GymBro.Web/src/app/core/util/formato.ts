/** Duración legible entre dos fechas ISO. Devuelve null si falta el fin o es inválida. */
export function duracionLegible(inicio: string, fin: string | null): string | null {
  if (!fin) return null;
  const minutos = Math.round((new Date(fin).getTime() - new Date(inicio).getTime()) / 60000);
  if (!Number.isFinite(minutos) || minutos < 0) return null;
  const h = Math.floor(minutos / 60);
  const m = minutos % 60;
  return h > 0 ? `${h} h ${m.toString().padStart(2, '0')} min` : `${m} min`;
}

/** Valor para un <input type="datetime-local"> en hora local. */
export function aDatetimeLocal(fecha: Date): string {
  const p = (n: number) => n.toString().padStart(2, '0');
  return `${fecha.getFullYear()}-${p(fecha.getMonth() + 1)}-${p(fecha.getDate())}T${p(fecha.getHours())}:${p(fecha.getMinutes())}`;
}

/** Convierte el valor de un datetime-local (hora local) a ISO UTC, como espera la API (timestamptz). */
export function datetimeLocalAIso(valor: string): string {
  return new Date(valor).toISOString();
}

/**
 * La API devuelve 0 en métricas que no se cargaron (por ejemplo, mediciones dadas de alta
 * solo con peso y altura). Para mostrarlas se tratan como "sin dato".
 */
export function oSinDato(valor: number | null | undefined): number | null {
  return valor === null || valor === undefined || valor === 0 ? null : valor;
}
