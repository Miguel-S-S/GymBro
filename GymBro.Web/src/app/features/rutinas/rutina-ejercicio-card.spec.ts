import { TestBed } from '@angular/core/testing';
import { RutinaEjercicio } from '../../core/models/acceso.models';
import { RutinaEjercicioCard } from './rutina-ejercicio-card';

function ejercicio(): RutinaEjercicio {
  return {
    id: 'e1', ejercicioId: 'ej1', orden: 2, ejercicio: 'Remo con barra', seriesObjetivo: 3, repeticionesMinimas: 8, repeticionesMaximas: 12,
    rirObjetivo: 2, descansoObjetivoSegundos: 90, observaciones: null,
  };
}

describe('RutinaEjercicioCard', () => {
  it('en modo lectura muestra el resumen de una línea y no ofrece edición', async () => {
    const fixture = TestBed.createComponent(RutinaEjercicioCard);
    fixture.componentRef.setInput('ejercicio', ejercicio());
    await fixture.whenStable();

    const texto = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(texto).toContain('Remo con barra');
    expect(texto).toContain('3 series × 8–12 reps · RIR 2 · 90 s');
    expect((fixture.nativeElement as HTMLElement).querySelectorAll('button').length).toBe(0);
  });

  it('en modo editable los botones +/− modifican el ejercicio', async () => {
    const e = ejercicio();
    const fixture = TestBed.createComponent(RutinaEjercicioCard);
    fixture.componentRef.setInput('ejercicio', e);
    fixture.componentRef.setInput('editable', true);
    await fixture.whenStable();

    const botones = (fixture.nativeElement as HTMLElement).querySelectorAll('button');
    expect(botones.length).toBe(10); // 5 campos × (−, +)
    (botones[1] as HTMLButtonElement).click(); // series +
    (botones[9] as HTMLButtonElement).click(); // descanso +
    expect(e.seriesObjetivo).toBe(4);
    expect(e.descansoObjetivoSegundos).toBe(105);
  });
});

// Etapa 7d: líneas generadas por el protocolo de piernas.
describe('RutinaEjercicioCard: protocolo de piernas', () => {
  async function texto(e: RutinaEjercicio) {
    const fixture = TestBed.createComponent(RutinaEjercicioCard);
    fixture.componentRef.setInput('ejercicio', e);
    await fixture.whenStable();
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  it('muestra rol, tempo excéntrico y la carga sugerida con la leyenda informativa', async () => {
    const t = await texto({ ...ejercicio(), rol: 'Referencia', grupo: 'Cuadriceps', tempoExcentricoSeg: 4, cargaSugeridaKg: 102.5 });
    expect(t).toContain('Referencia · tempo excéntrico 4 s');
    expect(t).toContain('Carga sugerida: 102,5 kg');
    expect(t).toContain('sugerencia informativa');
  });

  it('sin carga sugerida no muestra la línea de carga', async () => {
    const t = await texto({ ...ejercicio(), rol: 'Variable', tempoExcentricoSeg: 3, cargaSugeridaKg: null });
    expect(t).toContain('Variable · tempo excéntrico 3 s');
    expect(t).not.toContain('Carga sugerida');
  });

  it('una línea de otra rutina no muestra datos del protocolo', async () => {
    const t = await texto(ejercicio());
    expect(t).not.toContain('tempo excéntrico');
    expect(t).not.toContain('Carga sugerida');
  });
});

// Etapa 9a-1: pantallas de revisión y edición de rutina (usan esta tarjeta).
describe('RutinaEjercicioCard: tipo de medida', () => {
  async function crear(e: RutinaEjercicio, editable: boolean) {
    const fixture = TestBed.createComponent(RutinaEjercicioCard);
    fixture.componentRef.setInput('ejercicio', e);
    fixture.componentRef.setInput('editable', editable);
    await fixture.whenStable();
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  it('un ejercicio por segundos muestra la unidad en lectura y "Seg." al editar', async () => {
    const plancha: RutinaEjercicio = { ...ejercicio(), ejercicio: 'Plancha', seriesObjetivo: 2, repeticionesMinimas: 20, repeticionesMaximas: 40, tipoMedida: 'Segundos' };
    expect(await crear(plancha, false)).toContain('2 series × 20–40 s');

    const edicion = await crear({ ...plancha }, true);
    expect(edicion).toContain('Seg. mín.');
    expect(edicion).toContain('Seg. máx.');
    expect(edicion).toContain('20 s');
    expect(edicion).not.toContain('Reps');
  });

  it('un ejercicio por repeticiones conserva "reps" y "Reps mín."', async () => {
    const remo: RutinaEjercicio = { ...ejercicio(), tipoMedida: 'Repeticiones' };
    expect(await crear(remo, false)).toContain('3 series × 8–12 reps');
    const edicion = await crear({ ...remo }, true);
    expect(edicion).toContain('Reps mín.');
    expect(edicion).not.toContain('Seg.');
  });
});
