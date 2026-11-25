import { codigoEnBloques, trazoQr } from './qr';

// Etapa 11. Token sintetico con el mismo largo y alfabeto que el de la API (Base32, 60 caracteres).
const TOKEN = 'AEXVKZ5ZQAGGLM4M2KMK2Q7WIW3CSAAAAAAAAANTB2GL3YB6G2CPA7KZF2YQ';

describe('QR de asistencia (Etapa 11)', () => {
  it('genera una matriz cuadrada con modulos oscuros y margen de 4', () => {
    const t = trazoQr(TOKEN);
    expect(t.lado).toBeGreaterThanOrEqual(21);
    expect((t.lado - 17) % 4).toBe(0); // versiones del estandar: 21, 25, 29, ...
    expect(t.ladoConMargen).toBe(t.lado + 8);
    expect(t.d.startsWith('M4 4h1v1h-1z')).toBe(true); // esquina del patron de posicion
  });

  it('el mismo texto produce el mismo dibujo y otro texto, uno distinto', () => {
    expect(trazoQr(TOKEN).d).toBe(trazoQr(TOKEN).d);
    expect(trazoQr(TOKEN).d).not.toBe(trazoQr(TOKEN.replace('A', 'B')).d);
  });

  it('muestra el codigo en bloques de 4 para tipearlo', () => {
    expect(codigoEnBloques('ABCDEFGHIJ')).toBe('ABCD-EFGH-IJ');
    expect(codigoEnBloques('')).toBe('');
  });
});
