import { RutinaEjercicio } from '../models/acceso.models';
import { aplicarPaso, etiquetaEstado, resumenEjercicio } from './rutina-formato';

const base: RutinaEjercicio = {
  id: 'e1', orden: 1, ejercicio: 'Press de banca', seriesObjetivo: 3, repeticionesMinimas: 8, repeticionesMaximas: 12,
  rirObjetivo: 2, descansoObjetivoSegundos: 90, observaciones: null,
};

describe('resumenEjercicio', () => {
  it('resume la prescripción en una línea', () => {
    expect(resumenEjercicio(base)).toBe('3 series × 8–12 reps · RIR 2 · 90 s');
  });

  it('usa singular y un solo número cuando no hay rango', () => {
    expect(resumenEjercicio({ ...base, seriesObjetivo: 1, repeticionesMinimas: 10, repeticionesMaximas: 10 }))
      .toBe('1 serie × 10 reps · RIR 2 · 90 s');
  });

  it('omite el RIR cuando no está definido', () => {
    expect(resumenEjercicio({ ...base, rirObjetivo: null })).toBe('3 series × 8–12 reps · 90 s');
  });
});

describe('etiquetaEstado', () => {
  it('traduce los estados de la revisión', () => {
    expect(etiquetaEstado('PendienteRevision')).toBe('Pendiente de revisión');
    expect(etiquetaEstado('Aprobada')).toBe('Aprobada');
    expect(etiquetaEstado('Otro')).toBe('Otro');
  });
});

describe('aplicarPaso', () => {
  it('suma y resta respetando el paso', () => {
    expect(aplicarPaso(base, 'seriesObjetivo', 1).seriesObjetivo).toBe(4);
    expect(aplicarPaso(base, 'descansoObjetivoSegundos', 15).descansoObjetivoSegundos).toBe(105);
  });

  it('no deja las series por debajo de 1', () => {
    expect(aplicarPaso({ ...base, seriesObjetivo: 1 }, 'seriesObjetivo', -1).seriesObjetivo).toBe(1);
  });

  it('mantiene el rango de repeticiones coherente', () => {
    expect(aplicarPaso({ ...base, repeticionesMinimas: 12 }, 'repeticionesMinimas', 1).repeticionesMinimas).toBe(12);
    expect(aplicarPaso({ ...base, repeticionesMaximas: 8 }, 'repeticionesMaximas', -1).repeticionesMaximas).toBe(8);
  });

  it('limita el RIR entre 0 y 5', () => {
    expect(aplicarPaso({ ...base, rirObjetivo: 5 }, 'rirObjetivo', 1).rirObjetivo).toBe(5);
    expect(aplicarPaso({ ...base, rirObjetivo: 0 }, 'rirObjetivo', -1).rirObjetivo).toBe(0);
  });

  it('parte de 0 si el RIR no estaba definido', () => {
    expect(aplicarPaso({ ...base, rirObjetivo: null }, 'rirObjetivo', 1).rirObjetivo).toBe(1);
  });

  it('no baja el descanso de 15 s', () => {
    expect(aplicarPaso({ ...base, descansoObjetivoSegundos: 15 }, 'descansoObjetivoSegundos', -15).descansoObjetivoSegundos).toBe(15);
  });
});
