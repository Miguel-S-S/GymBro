import { EjercicioResumen } from '../../core/models/ejercicio.models';
import { RegistrarSesionRequest } from '../../core/models/sesion.models';
import { datetimeLocalAIso } from '../../core/util/formato';

/** Valor crudo del formulario de registro de sesión. */
export interface SesionFormValue {
  inicio: string;
  fin: string | null;
  caloriasQuemadas: number | null;
  frecuenciaCardiacaMaxima: number | null;
  frecuenciaCardiacaPromedio: number | null;
  ejercicios: {
    ejercicio: EjercicioResumen | null;
    observaciones: string | null;
    series: {
      pesoLevantadoKg: number | null;
      repeticiones: number | null;
      rir: number | null;
      esCalentamiento: boolean;
      tiempoDescansoSegundos: number | null;
    }[];
  }[];
}

const vacioANull = (v: number | null | undefined): number | null =>
  v === null || v === undefined || Number.isNaN(v) ? null : v;

/**
 * Arma el request de POST /api/Sesiones.
 * El orden de ejercicios y de series se toma de la posición en el formulario (base 1),
 * que es lo que valida el backend (OrdenSerie > 0 y orden > 0).
 */
export function construirRegistrarSesionRequest(socioId: string, v: SesionFormValue): RegistrarSesionRequest {
  return {
    socioId,
    // La API todavía no permite listar prescripciones: la sesión se registra como libre.
    prescripcionEntrenamientoId: null,
    inicio: datetimeLocalAIso(v.inicio),
    fin: v.fin ? datetimeLocalAIso(v.fin) : null,
    caloriasQuemadas: vacioANull(v.caloriasQuemadas),
    frecuenciaCardiacaMaxima: vacioANull(v.frecuenciaCardiacaMaxima),
    frecuenciaCardiacaPromedio: vacioANull(v.frecuenciaCardiacaPromedio),
    ejercicios: v.ejercicios.map((ej, i) => {
      if (!ej.ejercicio) throw new Error(`El ejercicio ${i + 1} no tiene un ejercicio del catálogo seleccionado.`);
      return {
        ejercicioId: ej.ejercicio.id,
        orden: i + 1,
        observaciones: ej.observaciones?.trim() ? ej.observaciones.trim() : null,
        seriesRealizadas: ej.series.map((s, j) => ({
          ordenSerie: j + 1,
          pesoLevantadoKg: s.pesoLevantadoKg ?? 0,
          repeticiones: s.repeticiones ?? 0,
          rir: vacioANull(s.rir),
          esCalentamiento: s.esCalentamiento,
          tiempoDescansoSegundos: vacioANull(s.tiempoDescansoSegundos),
        })),
      };
    }),
  };
}
