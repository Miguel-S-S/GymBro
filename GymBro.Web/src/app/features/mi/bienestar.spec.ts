import { RegistroBienestar } from '../../core/models/bienestar.models';
import {
  ajustar, alternar, construirPedido, fechaDelDispositivo, formularioDesdeRegistro, formularioVacio, motivoInvalido,
  numeroDeTexto, RANGOS, tieneAlgunDato,
} from './bienestar';

// Datos sintéticos de prueba.
const conEstres = (estres: number) => ({ ...formularioVacio(), estres });

describe('bienestar: lógica del registro diario (Etapa 8b)', () => {
  it('estrés y fatiga van de 1 a 7', () => {
    expect(motivoInvalido(conEstres(1))).toBeNull();
    expect(motivoInvalido(conEstres(7))).toBeNull();
    expect(motivoInvalido(conEstres(0))).toContain('1 (mejor) a 7 (peor)');
    expect(motivoInvalido(conEstres(8))).toContain('1 (mejor) a 7 (peor)');
    expect(motivoInvalido({ ...formularioVacio(), fatiga: 8 })).toContain('fatiga');
  });

  it('exige al menos un dato (una nota en blanco no cuenta)', () => {
    expect(motivoInvalido(formularioVacio())).toBe('Cargá al menos un dato.');
    expect(tieneAlgunDato({ ...formularioVacio(), nutricionNotas: '   ' })).toBe(false);
    expect(motivoInvalido({ ...formularioVacio(), nutricionNotas: 'Cena tarde' })).toBeNull();
  });

  it('valida los rangos de sueño, peso, agua, pulso y nota', () => {
    const f = formularioVacio();
    expect(motivoInvalido({ ...f, horasSueno: 7.25 })).toContain('media hora');
    expect(motivoInvalido({ ...f, horasSueno: 25 })).toContain('0 a 24');
    expect(motivoInvalido({ ...f, pesoKg: 70.25 })).toContain('un decimal');
    expect(motivoInvalido({ ...f, pesoKg: NaN })).toContain('peso');
    expect(motivoInvalido({ ...f, ingestaAguaLitros: 11 })).toContain('agua');
    expect(motivoInvalido({ ...f, frecuenciaCardiacaReposo: 29 })).toContain('30 a 120');
    expect(motivoInvalido({ ...f, frecuenciaCardiacaReposo: 121 })).toContain('30 a 120');
    expect(motivoInvalido({ ...f, nutricionNotas: 'x'.repeat(501) })).toContain('500');
    expect(motivoInvalido({ ...f, horasSueno: 24, pesoKg: 400, ingestaAguaLitros: 0, frecuenciaCardiacaReposo: 30 })).toBeNull();
  });

  it('los botones − / + parten de un valor típico y no salen del rango', () => {
    expect(ajustar(null, 0.5, RANGOS.sueno)).toBe(7);
    expect(ajustar(7, 0.5, RANGOS.sueno)).toBe(7.5);
    expect(ajustar(0, -0.5, RANGOS.sueno)).toBe(0);
    expect(ajustar(24, 0.5, RANGOS.sueno)).toBe(24);
    expect(ajustar(null, -0.5, RANGOS.agua)).toBe(1.5);
  });

  it('tocar el nivel elegido lo quita', () => {
    expect(alternar<number>(null, 3)).toBe(3);
    expect(alternar<number>(3, 3)).toBeNull();
    expect(alternar<number>(3, 5)).toBe(5);
  });

  it('convierte el texto de los campos numéricos (coma o punto; vacío = sin dato)', () => {
    expect(numeroDeTexto('72,5')).toBe(72.5);
    expect(numeroDeTexto(' 60 ')).toBe(60);
    expect(numeroDeTexto('')).toBeNull();
    expect(numeroDeTexto('abc')).toBeNaN();
  });

  it('el pedido envía todo el registro (lo vacío como null) y recorta la nota', () => {
    const pedido = construirPedido({ ...formularioVacio(), estres: 3, nutricionCategoria: 'Alta', nutricionNotas: '  Asado  ' });
    expect(pedido).toEqual({
      horasSueno: null, estres: 3, fatiga: null, pesoKg: null, nutricionCategoria: 'Alta',
      ingestaAguaLitros: null, frecuenciaCardiacaReposo: null, nutricionNotas: 'Asado',
    });
    expect(construirPedido({ ...formularioVacio(), fatiga: 2 }).nutricionNotas).toBeNull();
    expect(() => construirPedido(formularioVacio())).toThrow('al menos un dato');
  });

  it('carga un registro existente para editarlo', () => {
    const registro: RegistroBienestar = {
      id: 'r1', socioId: 's1', fecha: '2026-11-17', horasSueno: 6.5, estres: 4, fatiga: 5, pesoKg: null,
      nutricionCategoria: 'Baja', ingestaAguaLitros: 2, nutricionNotas: null, frecuenciaCardiacaReposo: 58,
      fechaAlta: '2026-11-17T12:00:00Z', fechaModificacion: null,
    };
    expect(formularioDesdeRegistro(registro)).toEqual({
      horasSueno: 6.5, estres: 4, fatiga: 5, pesoKg: null, nutricionCategoria: 'Baja', ingestaAguaLitros: 2,
      frecuenciaCardiacaReposo: 58, nutricionNotas: '',
    });
  });

  it('la fecha del dispositivo usa el día local', () => {
    expect(fechaDelDispositivo(new Date(2026, 10, 17, 23, 30))).toBe('2026-11-17');
  });
});
