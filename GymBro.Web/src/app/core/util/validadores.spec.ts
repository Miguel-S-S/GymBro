import { FormControl, FormGroup } from '@angular/forms';
import { clavesCoinciden, nacimientoValido, validarNacimiento } from './validadores';

describe('validadores de la activación de cuenta (etapa 6)', () => {
  const hoy = new Date(2026, 10, 12);

  it('nacimientoValido acepta una fecha de una persona de 14 años o más', () => {
    expect(nacimientoValido('2012-11-12', hoy)).toBe(true); // cumple 14 hoy
    expect(nacimientoValido('1995-03-14', hoy)).toBe(true);
  });

  it('nacimientoValido rechaza menores de 14, fechas futuras, formatos raros y días inexistentes', () => {
    expect(nacimientoValido('2012-11-13', hoy)).toBe(false); // cumple 14 mañana
    expect(nacimientoValido('2030-01-01', hoy)).toBe(false);
    expect(nacimientoValido('14/03/1995', hoy)).toBe(false);
    expect(nacimientoValido('1995-02-30', hoy)).toBe(false);
    expect(nacimientoValido('1850-01-01', hoy)).toBe(false);
    expect(nacimientoValido('', hoy)).toBe(false);
  });

  it('validarNacimiento deja pasar el campo vacío (lo cubre required) y marca el inválido', () => {
    expect(validarNacimiento(new FormControl(''))).toBeNull();
    expect(validarNacimiento(new FormControl('2999-01-01'))).toEqual({ nacimientoInvalido: true });
  });

  it('clavesCoinciden marca el grupo cuando las contraseñas difieren', () => {
    const grupo = (a: string, b: string) => new FormGroup({ password: new FormControl(a), repetir: new FormControl(b) });
    expect(clavesCoinciden(grupo('Clave-1234', 'Clave-1234'))).toBeNull();
    expect(clavesCoinciden(grupo('Clave-1234', 'Clave-9999'))).toEqual({ noCoinciden: true });
    expect(clavesCoinciden(grupo('Clave-1234', ''))).toBeNull(); // el vacío lo cubre required
  });
});
