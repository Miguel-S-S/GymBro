import { borrarBorradorBienestar, guardarBorradorBienestar, leerBorradorBienestar } from './borrador-bienestar';
import { borrarTodosLosBorradores, guardarBorrador, leerBorrador, PREFIJO_BORRADOR_BIENESTAR } from './borrador-sesion';

/** Almacenamiento en memoria con la interfaz de Storage. */
function memoria(): Storage {
  const datos = new Map<string, string>();
  return {
    get length() { return datos.size; },
    clear: () => datos.clear(),
    getItem: (k: string) => datos.get(k) ?? null,
    key: (i: number) => [...datos.keys()][i] ?? null,
    removeItem: (k: string) => { datos.delete(k); },
    setItem: (k: string, v: string) => { datos.set(k, v); },
  };
}

describe('borrador de bienestar (Etapa 8b)', () => {
  it('guarda y recupera el borrador del día', () => {
    const s = memoria();
    guardarBorradorBienestar('s1', { fecha: '2026-11-17', formulario: { estres: 3 } }, s);
    expect(leerBorradorBienestar<{ estres: number }>('s1', '2026-11-17', s)?.formulario.estres).toBe(3);
  });

  it('no aplica el borrador de otro día', () => {
    const s = memoria();
    guardarBorradorBienestar('s1', { fecha: '2026-11-16', formulario: { estres: 3 } }, s);
    expect(leerBorradorBienestar('s1', '2026-11-17', s)).toBeNull();
    expect(leerBorradorBienestar('s1', undefined, s)?.fecha).toBe('2026-11-16'); // sin conexión: el que haya
  });

  it('contenido corrupto o almacenamiento bloqueado no rompen la pantalla', () => {
    const s = memoria();
    s.setItem(PREFIJO_BORRADOR_BIENESTAR + 's1', '{no es json');
    expect(leerBorradorBienestar('s1', undefined, s)).toBeNull();
    const bloqueado = { getItem: () => { throw new Error('bloqueado'); }, setItem: () => { throw new Error('bloqueado'); }, removeItem: () => { throw new Error('bloqueado'); } };
    expect(() => guardarBorradorBienestar('s1', { fecha: 'x', formulario: {} }, bloqueado)).not.toThrow();
    expect(leerBorradorBienestar('s1', undefined, bloqueado)).toBeNull();
    expect(() => borrarBorradorBienestar('s1', bloqueado)).not.toThrow();
  });

  it('cerrar sesión borra también los borradores de bienestar (datos de salud)', () => {
    const s = memoria();
    guardarBorradorBienestar('s1', { fecha: '2026-11-17', formulario: { estres: 3 } }, s);
    guardarBorrador('s1', { series: [] }, s);
    s.setItem('gymbro.otra-cosa', 'x');
    borrarTodosLosBorradores(s);
    expect(leerBorradorBienestar('s1', undefined, s)).toBeNull();
    expect(leerBorrador('s1', s)).toBeNull();
    expect(s.getItem('gymbro.otra-cosa')).toBe('x');
  });
});
