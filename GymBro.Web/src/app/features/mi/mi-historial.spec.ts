import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { EjerciciosApi } from '../../core/api/ejercicios.api';
import { SesionesApi } from '../../core/api/sesiones.api';
import { ComparacionSesion, Sesion } from '../../core/models/sesion.models';
import { MiSesionDetalle } from './mi-historial';

// Etapa 9a-1. Datos sintéticos de prueba: plancha (segundos) y crunch (repeticiones) en la misma sesión.
const SESION: Sesion = {
  id: 'ses1', socioId: 's1', prescripcionEntrenamientoId: 'p1', inicio: '2026-11-17T18:00:00Z', fin: '2026-11-17T19:00:00Z',
  caloriasQuemadas: 0, frecuenciaCardiacaMaxima: 0, frecuenciaCardiacaPromedio: 0, ejercicios: [],
};
const serie = (valor: number, cumple: boolean, unidad: string) => ({
  ordenSerie: 1, pesoLevantadoKg: 0, repeticiones: valor, rir: null, esCalentamiento: false, tiempoDescansoSegundos: null, volumen: 0,
  repeticionesComparacion: { cumpleObjetivo: cumple, desviacion: 0, mensaje: cumple ? 'Dentro del rango' : `Faltaron 1 ${unidad}.` },
  rirComparacion: null,
});
const ejecucion = (valor: number, unidad: string) => ({
  cantidadSeriesTotales: 1, cantidadSeriesEfectivas: 1, cantidadSeriesCalentamiento: 0,
  volumenTotalKg: 0, volumenEfectivoKg: 0, volumenCalentamientoKg: 0, detallesSeries: [serie(valor, true, unidad)],
});
const comparacionSeries = { series: { prescritas: 2, realizadasEfectivas: 1, diferencia: -1, mensaje: 'Faltaron 1 series' } };
const COMPARACION: ComparacionSesion = {
  sesionId: 'ses1', prescripcionEntrenamientoId: 'p1',
  ejercicios: [
    { ejercicioId: 'ej-p', orden: 1, tipoMedida: 'Segundos', comparacion: comparacionSeries, ejecucion: ejecucion(30, 's'),
      prescripcion: { seriesObjetivo: 2, repeticionesMinimas: 20, repeticionesMaximas: 40, rirObjetivo: 2, descansoObjetivoSegundos: 60 } },
    { ejercicioId: 'ej-c', orden: 2, tipoMedida: 'Repeticiones', comparacion: comparacionSeries, ejecucion: ejecucion(12, 'reps'),
      prescripcion: { seriesObjetivo: 3, repeticionesMinimas: 10, repeticionesMaximas: 15, rirObjetivo: 2, descansoObjetivoSegundos: 60 } },
  ],
};
const API_SESIONES = { obtener: () => of(SESION), comparacion: () => of(COMPARACION) };
const API_EJERCICIOS = { nombres: () => of({ 'ej-p': 'Plancha', 'ej-c': 'Crunch abdominal' }) };

describe('MiSesionDetalle (Socio): tipo de medida', () => {
  it('muestra segundos para la plancha y repeticiones para el crunch', async () => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: SesionesApi, useValue: API_SESIONES }, { provide: EjerciciosApi, useValue: API_EJERCICIOS }],
    });
    const fixture = TestBed.createComponent(MiSesionDetalle);
    fixture.componentRef.setInput('id', 'ses1');
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const [plancha, crunch] = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('section.ejercicio'));
    const texto = (el: Element) => (el.textContent ?? '').replace(/\s+/g, ' ');
    expect(texto(plancha)).toContain('Objetivo: 2 × 20–40 s');
    expect(texto(plancha)).toContain('0 kg × 30 s');
    expect(texto(plancha)).not.toContain('Volumen efectivo'); // no se multiplica kg por segundos

    expect(texto(crunch)).toContain('Objetivo: 3 × 10–15 reps');
    expect(texto(crunch)).toContain('0 kg × 12');
    expect(texto(crunch)).toContain('Volumen efectivo');
  });
});
