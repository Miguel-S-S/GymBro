import { EjercicioComparado } from '../models/sesion.models';
import { indicadoresCumplimiento } from './cumplimiento';

// Etapa 8e. Datos sinteticos de prueba: funcion compartida por la vista del Socio y la del personal.
function ejercicio(reps: number[], calentamiento: boolean[] = [], prescripcion = true): EjercicioComparado {
  return {
    ejercicioId: 'e', orden: 1, tipoMedida: 'Repeticiones',
    prescripcion: prescripcion ? { seriesObjetivo: 3, repeticionesMinimas: 8, repeticionesMaximas: 12, rirObjetivo: null, descansoObjetivoSegundos: 60 } : null,
    comparacion: { series: { prescritas: 3, realizadasEfectivas: reps.length, diferencia: 0, mensaje: '' } },
    ejecucion: {
      cantidadSeriesTotales: reps.length, cantidadSeriesEfectivas: reps.length, cantidadSeriesCalentamiento: 0,
      volumenTotalKg: 0, volumenEfectivoKg: 0, volumenCalentamientoKg: 0,
      detallesSeries: reps.map((r, i) => ({
        ordenSerie: i + 1, pesoLevantadoKg: 10, repeticiones: r, rir: null, esCalentamiento: calentamiento[i] ?? false,
        tiempoDescansoSegundos: null, volumen: 10 * r, repeticionesComparacion: null, rirComparacion: null,
      })),
    },
  } as EjercicioComparado;
}

describe('indicadoresCumplimiento (Etapa 8e, compartida)', () => {
  it('los extremos del rango cuentan como dentro', () => {
    expect(indicadoresCumplimiento(ejercicio([8, 12, 13, 7]))).toEqual({ series: 4, enRango: 2, etiquetaRango: 'Repeticiones' });
  });

  it('sin prescripcion no hay series en rango', () => {
    expect(indicadoresCumplimiento(ejercicio([10, 10], [], false)).enRango).toBe(0);
  });

  it('excluye el calentamiento', () => {
    expect(indicadoresCumplimiento(ejercicio([10, 10, 10], [true, false, false]))).toEqual(
      { series: 2, enRango: 2, etiquetaRango: 'Repeticiones' });
  });
});
