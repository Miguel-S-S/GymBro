import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/** El campo `repetir` debe coincidir con `clave` (error `noCoinciden` en el grupo). */
export const clavesCoinciden: ValidatorFn = (grupo: AbstractControl): ValidationErrors | null => {
  const clave = grupo.get('password')?.value;
  const repetir = grupo.get('repetir')?.value;
  return clave && repetir && clave !== repetir ? { noCoinciden: true } : null;
};

/** Edad mínima para usar la aplicación (la misma que exige la API). */
export const EDAD_MINIMA = 14;

/** Fecha yyyy-MM-dd válida, no futura y de una persona de al menos EDAD_MINIMA años. */
export function nacimientoValido(valor: string, hoy: Date = new Date()): boolean {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(valor)) return false;
  const [a, m, d] = valor.split('-').map(Number);
  const fecha = new Date(a, m - 1, d);
  if (fecha.getFullYear() !== a || fecha.getMonth() !== m - 1 || fecha.getDate() !== d) return false;
  const limite = new Date(hoy.getFullYear() - EDAD_MINIMA, hoy.getMonth(), hoy.getDate());
  return fecha <= limite && a >= 1900;
}

export const validarNacimiento: ValidatorFn = (c: AbstractControl): ValidationErrors | null =>
  !c.value || nacimientoValido(c.value) ? null : { nacimientoInvalido: true };
