import { borrarBorrador, borrarTodosLosBorradores, guardarBorrador, leerBorrador, PREFIJO_BORRADOR } from './borrador-sesion';

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

describe('borrador de sesión', () => {
  it('guarda y recupera el borrador del socio (sobrevive a un recargo)', () => {
    const s = memoria();
    guardarBorrador('socio-1', { prescripcionId: 'p1', series: [1, 2] }, s);

    expect(leerBorrador<{ prescripcionId: string }>('socio-1', s)?.prescripcionId).toBe('p1');
    expect(leerBorrador('socio-2', s)).toBeNull();
  });

  it('al finalizar se borra el borrador del socio', () => {
    const s = memoria();
    guardarBorrador('socio-1', { a: 1 }, s);
    borrarBorrador('socio-1', s);
    expect(leerBorrador('socio-1', s)).toBeNull();
  });

  it('al cerrar sesión se borran todos los borradores y nada más', () => {
    const s = memoria();
    guardarBorrador('socio-1', { a: 1 }, s);
    guardarBorrador('socio-2', { a: 2 }, s);
    s.setItem('gymbro.sesion', 'token');

    borrarTodosLosBorradores(s);

    expect(leerBorrador('socio-1', s)).toBeNull();
    expect(leerBorrador('socio-2', s)).toBeNull();
    expect(s.getItem('gymbro.sesion')).toBe('token');
  });

  it('ignora un contenido corrupto', () => {
    const s = memoria();
    s.setItem(PREFIJO_BORRADOR + 'socio-1', '{no es json');
    expect(leerBorrador('socio-1', s)).toBeNull();
  });

  it('no falla si el almacenamiento no está disponible', () => {
    const roto = {
      length: 0,
      getItem: () => { throw new Error('bloqueado'); },
      setItem: () => { throw new Error('bloqueado'); },
      removeItem: () => { throw new Error('bloqueado'); },
      key: () => null,
    };
    expect(() => guardarBorrador('s', { a: 1 }, roto)).not.toThrow();
    expect(leerBorrador('s', roto)).toBeNull();
    expect(() => borrarBorrador('s', roto)).not.toThrow();
    expect(() => borrarTodosLosBorradores(null)).not.toThrow();
  });
});
