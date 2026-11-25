import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { EjerciciosApi } from '../../core/api/ejercicios.api';
import { SesionesApi } from '../../core/api/sesiones.api';
import { ComparacionSesion, Sesion } from '../../core/models/sesion.models';
import { SesionDetalle } from './sesion-detalle';

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

describe('SesionDetalle (personal): tipo de medida', () => {
  it('muestra segundos para la plancha y repeticiones para el crunch', async () => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: SesionesApi, useValue: API_SESIONES }, { provide: EjerciciosApi, useValue: API_EJERCICIOS }],
    });
    const fixture = TestBed.createComponent(SesionDetalle);
    fixture.componentRef.setInput('id', 'ses1');
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const [plancha, crunch] = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('section.ejercicio'));
    expect(plancha.textContent).toContain('Prescrito: 2 series de 20–40 s');
    expect(plancha.querySelector('th.mat-column-reps')!.textContent).toContain('Seg.');
    expect(crunch.textContent).toContain('Prescrito: 3 series de 10–15 reps');
    expect(crunch.querySelector('th.mat-column-reps')!.textContent).toContain('Reps');
  });
});

// Etapa 8e. Datos sinteticos de prueba: prensa con rango 8-12 y objetivo de 3 series.
type SerieDato = [peso: number, reps: number, calentamiento?: boolean];
const PRENSA = { seriesObjetivo: 3, repeticionesMinimas: 8, repeticionesMaximas: 12, rirObjetivo: 2, descansoObjetivoSegundos: 90 };

function prensa(series: SerieDato[] | null, tipoMedida: 'Repeticiones' | 'Segundos' = 'Repeticiones', mensaje = 'Cumple objetivo') {
  const detalles = (series ?? []).map(([peso, reps, cal = false], i) => ({
    ordenSerie: i + 1, pesoLevantadoKg: peso, repeticiones: reps, rir: null, esCalentamiento: cal,
    tiempoDescansoSegundos: null, volumen: peso * reps,
    repeticionesComparacion: cal ? null
      : { cumpleObjetivo: reps >= 8 && reps <= 12, desviacion: 0, mensaje: reps >= 8 && reps <= 12 ? 'Dentro del rango' : 'Fuera' },
    rirComparacion: null,
  }));
  const efectivas = detalles.filter((d) => !d.esCalentamiento).length;
  return {
    ejercicioId: 'ej-prensa', orden: 1, tipoMedida, prescripcion: PRENSA,
    // El veredicto anterior sigue en el contrato (obsoleto); la pantalla no debe mostrarlo.
    comparacion: { series: { prescritas: 3, realizadasEfectivas: efectivas, diferencia: efectivas - 3, mensaje } },
    ejecucion: series === null ? null : {
      cantidadSeriesTotales: detalles.length, cantidadSeriesEfectivas: efectivas, cantidadSeriesCalentamiento: detalles.length - efectivas,
      volumenTotalKg: 0, volumenEfectivoKg: 0, volumenCalentamientoKg: 0, detallesSeries: detalles,
    },
  };
}

async function textoPersonal(ej: ReturnType<typeof prensa>) {
  const comparacion: ComparacionSesion = { sesionId: 'ses1', prescripcionEntrenamientoId: 'p1', ejercicios: [ej] };
  TestBed.configureTestingModule({
    providers: [provideRouter([]),
      { provide: SesionesApi, useValue: { obtener: () => of(SESION), comparacion: () => of(comparacion) } },
      { provide: EjerciciosApi, useValue: { nombres: () => of({ 'ej-prensa': 'Prensa de piernas' }) } }],
  });
  const fixture = TestBed.createComponent(SesionDetalle);
  fixture.componentRef.setInput('id', 'ses1');
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
  return ((fixture.nativeElement as HTMLElement).textContent ?? '').replace(/\s+/g, ' ');
}

describe('SesionDetalle (Dueño y Personal Trainer): indicadores de cumplimiento (Etapa 8e)', () => {
  it('3 de 3 series con 8, 8 y 5 (rango 8-12): dos de tres en rango y sin "Cumple objetivo"', async () => {
    const t = await textoPersonal(prensa([[58, 8], [59, 8], [75, 5]]));
    expect(t).toContain('Series: 3 de 3');
    expect(t).toContain('Repeticiones en rango: 2 de 3 series');
    expect(t).not.toMatch(/cumple objetivo/i);
  });

  it('4 series con objetivo 3: "Series: 4 de 3" y sin "Excedieron 1 series"', async () => {
    const t = await textoPersonal(prensa([[50, 10], [50, 10], [50, 9], [50, 8]], 'Repeticiones', 'Excedieron 1 series'));
    expect(t).toContain('Series: 4 de 3');
    expect(t).toContain('Repeticiones en rango: 4 de 4 series');
    expect(t).not.toMatch(/excedieron|cumple objetivo/i);
  });

  it('2 series con objetivo 3: "Series: 2 de 3" y sin "Faltaron 1 series"', async () => {
    const t = await textoPersonal(prensa([[50, 10], [50, 9]], 'Repeticiones', 'Faltaron 1 series'));
    expect(t).toContain('Series: 2 de 3');
    expect(t).not.toMatch(/faltaron/i);
  });

  it('ejercicio prescrito sin ejecutar: "Series: 0 de 3" y sin indicador de repeticiones', async () => {
    const t = await textoPersonal(prensa(null, 'Repeticiones', 'Faltaron 3 series'));
    expect(t).toContain('Series: 0 de 3');
    expect(t).not.toContain('en rango');
    expect(t).toContain('Este ejercicio estaba prescrito pero no se ejecutó.');
  });

  it('las series de calentamiento no cuentan en ninguno de los dos indicadores', async () => {
    const t = await textoPersonal(prensa([[30, 15, true], [20, 10, true], [60, 9], [60, 6]]));
    expect(t).toContain('Series: 2 de 3');
    expect(t).toContain('Repeticiones en rango: 1 de 2 series');
  });

  it('en ejercicios por tiempo el indicador dice "Segundos en rango"', async () => {
    const t = await textoPersonal(prensa([[0, 10], [0, 30]], 'Segundos'));
    expect(t).toContain('Segundos en rango: 1 de 2 series');
  });
});
