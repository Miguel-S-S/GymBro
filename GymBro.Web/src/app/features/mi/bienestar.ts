import { NutricionCategoria, RegistroBienestar, RegistroBienestarRequest } from '../../core/models/bienestar.models';

/**
 * Registro de bienestar del día (Etapa 8b): lógica pura de la pantalla /mi/bienestar.
 * Los rangos replican los del servidor (GymBro.Domain.Entities.RangosBienestar); el servidor vuelve a validar.
 */
export const RANGOS = {
  sueno: { min: 0, max: 24, paso: 0.5, inicial: 7 },
  escala: { min: 1, max: 7 },
  peso: { min: 1, max: 400 },
  agua: { min: 0, max: 10, paso: 0.5, inicial: 1.5 },
  /** Decisión de diseño de la Etapa 8b, no un criterio clínico. */
  fc: { min: 30, max: 120 },
  notas: 500,
} as const;

/** Anclas de texto de la escala 1–7 estilo Hooper (1 = mejor, 7 = peor). Redacción propia. */
export const ANCLAS = {
  estres: { min: 'Sin estrés', max: 'Estrés máximo' },
  fatiga: { min: 'Sin fatiga', max: 'Fatiga máxima' },
} as const;

export const CATEGORIAS_NUTRICION: readonly { valor: NutricionCategoria; etiqueta: string }[] = [
  { valor: 'Baja', etiqueta: 'Menos de lo habitual' },
  { valor: 'Habitual', etiqueta: 'Lo habitual' },
  { valor: 'Alta', etiqueta: 'Más de lo habitual' },
];

export const NIVELES_ESCALA = [1, 2, 3, 4, 5, 6, 7] as const;

export interface FormularioBienestar {
  horasSueno: number | null;
  estres: number | null;
  fatiga: number | null;
  pesoKg: number | null;
  nutricionCategoria: NutricionCategoria | null;
  ingestaAguaLitros: number | null;
  frecuenciaCardiacaReposo: number | null;
  nutricionNotas: string;
}

export function formularioVacio(): FormularioBienestar {
  return { horasSueno: null, estres: null, fatiga: null, pesoKg: null, nutricionCategoria: null,
    ingestaAguaLitros: null, frecuenciaCardiacaReposo: null, nutricionNotas: '' };
}

export function formularioDesdeRegistro(r: RegistroBienestar): FormularioBienestar {
  return {
    horasSueno: r.horasSueno, estres: r.estres, fatiga: r.fatiga, pesoKg: r.pesoKg,
    nutricionCategoria: r.nutricionCategoria, ingestaAguaLitros: r.ingestaAguaLitros,
    frecuenciaCardiacaReposo: r.frecuenciaCardiacaReposo, nutricionNotas: r.nutricionNotas ?? '',
  };
}

/**
 * Botones − / + de sueño y agua (uso con una mano, sin teclado): el primer toque parte de un valor típico;
 * después suma o resta de a `paso`, sin salir del rango.
 */
export function ajustar(actual: number | null, delta: number, r: { min: number; max: number; inicial: number }): number {
  if (actual === null) return r.inicial;
  return Math.min(r.max, Math.max(r.min, Math.round((actual + delta) * 10) / 10));
}

/** Tocar el nivel ya elegido lo quita (vuelve a "sin dato"). */
export function alternar<T>(actual: T | null, elegido: T): T | null {
  return actual === elegido ? null : elegido;
}

/** Convierte el texto de un campo numérico ("" = sin dato; coma o punto decimal). */
export function numeroDeTexto(texto: string): number | null {
  const limpio = texto.trim().replace(',', '.');
  if (limpio === '') return null;
  const n = Number(limpio);
  return Number.isFinite(n) ? n : NaN;
}

const unDecimal = (n: number) => Math.round(n * 10) === n * 10;

/** Motivo por el que no se puede enviar, o null si el formulario es válido. */
export function motivoInvalido(f: FormularioBienestar): string | null {
  const { sueno, escala, peso, agua, fc, notas } = RANGOS;
  if (f.horasSueno !== null && (f.horasSueno < sueno.min || f.horasSueno > sueno.max || (f.horasSueno * 2) % 1 !== 0))
    return 'Las horas de sueño van de 0 a 24, de a media hora.';
  for (const [valor, nombre] of [[f.estres, 'estrés'], [f.fatiga, 'fatiga']] as const)
    if (valor !== null && (!Number.isInteger(valor) || valor < escala.min || valor > escala.max))
      return `El nivel de ${nombre} va de 1 (mejor) a 7 (peor).`;
  if (f.pesoKg !== null && (Number.isNaN(f.pesoKg) || f.pesoKg < peso.min || f.pesoKg > peso.max || !unDecimal(f.pesoKg)))
    return `El peso va de ${peso.min} a ${peso.max} kg, con un decimal como máximo.`;
  if (f.ingestaAguaLitros !== null && (f.ingestaAguaLitros < agua.min || f.ingestaAguaLitros > agua.max || !unDecimal(f.ingestaAguaLitros)))
    return `El agua va de ${agua.min} a ${agua.max} litros.`;
  if (f.frecuenciaCardiacaReposo !== null &&
      (!Number.isInteger(f.frecuenciaCardiacaReposo) || f.frecuenciaCardiacaReposo < fc.min || f.frecuenciaCardiacaReposo > fc.max))
    return `La frecuencia cardíaca en reposo va de ${fc.min} a ${fc.max} latidos por minuto.`;
  if (f.nutricionNotas.trim().length > notas) return `La nota admite hasta ${notas} caracteres.`;
  if (!tieneAlgunDato(f)) return 'Cargá al menos un dato.';
  return null;
}

export function tieneAlgunDato(f: FormularioBienestar): boolean {
  return f.horasSueno !== null || f.estres !== null || f.fatiga !== null || f.pesoKg !== null ||
    f.nutricionCategoria !== null || f.ingestaAguaLitros !== null || f.frecuenciaCardiacaReposo !== null ||
    f.nutricionNotas.trim() !== '';
}

/** Pedido para PUT /api/bienestar/{fecha}: reemplaza el registro del día completo. */
export function construirPedido(f: FormularioBienestar): RegistroBienestarRequest {
  const motivo = motivoInvalido(f);
  if (motivo) throw new Error(motivo);
  const nota = f.nutricionNotas.trim();
  return {
    horasSueno: f.horasSueno, estres: f.estres, fatiga: f.fatiga, pesoKg: f.pesoKg,
    nutricionCategoria: f.nutricionCategoria, ingestaAguaLitros: f.ingestaAguaLitros,
    frecuenciaCardiacaReposo: f.frecuenciaCardiacaReposo, nutricionNotas: nota === '' ? null : nota,
  };
}

/** Fecha local del dispositivo (yyyy-MM-dd): solo para el borrador cuando no hay conexión al abrir la pantalla. */
export function fechaDelDispositivo(ahora = new Date()): string {
  const d = (n: number) => String(n).padStart(2, '0');
  return `${ahora.getFullYear()}-${d(ahora.getMonth() + 1)}-${d(ahora.getDate())}`;
}
