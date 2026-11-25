import qrcode from 'qrcode-generator';

/** Matriz de un QR dibujada como un único trazo SVG (sin innerHTML ni sanitizador). */
export interface TrazoQr {
  /** Módulos por lado, sin margen. */
  lado: number;
  /** Atributo d de un <path>: un cuadrado de 1 x 1 por módulo oscuro, desplazado por el margen. */
  d: string;
  /** Lado total con el margen (para el viewBox). */
  ladoConMargen: number;
}

/**
 * Etapa 11: el QR del Socio se dibuja en el cliente (no viaja ninguna imagen). El token es Base32 en mayúsculas, que
 * entra en el modo alfanumérico del QR; corrección de errores M. Margen de 4 módulos (zona silenciosa del estándar).
 */
export function trazoQr(texto: string, margen = 4): TrazoQr {
  const qr = qrcode(0, 'M');
  qr.addData(texto, /^[0-9A-Z $%*+\-./:]*$/.test(texto) ? 'Alphanumeric' : 'Byte');
  qr.make();
  const lado = qr.getModuleCount();
  const partes: string[] = [];
  for (let f = 0; f < lado; f++) {
    for (let c = 0; c < lado; c++) {
      if (qr.isDark(f, c)) partes.push(`M${c + margen} ${f + margen}h1v1h-1z`);
    }
  }
  return { lado, d: partes.join(''), ladoConMargen: lado + 2 * margen };
}

/** Código para leer en voz alta o tipear: bloques de 4 caracteres separados por guiones (la API los ignora). */
export function codigoEnBloques(token: string): string {
  return (token.match(/.{1,4}/g) ?? []).join('-');
}
