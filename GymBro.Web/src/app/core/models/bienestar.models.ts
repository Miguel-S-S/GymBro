/** Ingesta del día respecto de lo habitual (GymBro.Domain.Enums.NutricionCategoria). */
export type NutricionCategoria = 'Baja' | 'Habitual' | 'Alta';

/** GymBro.Application.DTOs.Bienestar.RegistroBienestarDto */
export interface RegistroBienestar {
  id: string;
  socioId: string;
  /** Día local del socio, yyyy-MM-dd. */
  fecha: string;
  horasSueno: number | null;
  estres: number | null;
  fatiga: number | null;
  pesoKg: number | null;
  nutricionCategoria: NutricionCategoria | null;
  ingestaAguaLitros: number | null;
  nutricionNotas: string | null;
  frecuenciaCardiacaReposo: number | null;
  fechaAlta: string;
  fechaModificacion: string | null;
}

/** GymBro.Application.DTOs.Bienestar.RegistroBienestarRequest (todo opcional; al menos un dato). */
export interface RegistroBienestarRequest {
  horasSueno: number | null;
  estres: number | null;
  fatiga: number | null;
  pesoKg: number | null;
  nutricionCategoria: NutricionCategoria | null;
  ingestaAguaLitros: number | null;
  nutricionNotas: string | null;
  frecuenciaCardiacaReposo: number | null;
}

/** GymBro.Application.DTOs.Bienestar.BienestarHoyDto */
export interface BienestarHoy {
  /** Día local calculado en el servidor (yyyy-MM-dd). */
  fechaLocal: string;
  registro: RegistroBienestar | null;
}
