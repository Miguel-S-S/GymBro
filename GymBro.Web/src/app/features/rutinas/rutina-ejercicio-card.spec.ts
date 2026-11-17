import { TestBed } from '@angular/core/testing';
import { RutinaEjercicio } from '../../core/models/acceso.models';
import { RutinaEjercicioCard } from './rutina-ejercicio-card';

function ejercicio(): RutinaEjercicio {
  return {
    id: 'e1', orden: 2, ejercicio: 'Remo con barra', seriesObjetivo: 3, repeticionesMinimas: 8, repeticionesMaximas: 12,
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
