/** GymBro.Application.DTOs.MedicionResponseDto */
export interface Medicion {
  id: string;
  socioId: string;
  fechaAlta: string;
  pesoKg: number;
  alturaCm: number;
  porcentajeGrasa: number;
  imc: number;
  frecuenciaEntrenamientoDias: number;
  tipoEntrenamiento: 'Fuerza' | 'Cardio' | 'HIIT' | null;
  ingestaAguaLitros: number;
  frecuenciaCardiacaReposo: number;
  presionArterialSistolica: number;
  presionArterialDiastolica: number;
  presionArterialMedia: number;
  condicionMedica: string;
  nivelEstres: number;
  horasSuenoUltimaNoche: number;
}

/** GymBro.Application.DTOs.MedicionCreateDto (la API hoy solo acepta peso y altura). */
export interface MedicionCreate {
  socioId: string;
  pesoKg: number;
  alturaCm: number;
}
