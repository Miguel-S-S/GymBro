import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { EjerciciosApi } from '../../core/api/ejercicios.api';
import { SesionesApi } from '../../core/api/sesiones.api';
import { AuthService } from '../../core/auth/auth.service';
import { ComparacionSesion, Sesion } from '../../core/models/sesion.models';
import { MiHistorial, MiSesionDetalle } from './mi-historial';

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

// Correccion Etapa 8: conteos del listado. Datos sinteticos: press en dos posiciones (cuenta una vez) y sentadilla;
// 1 serie de calentamiento + 4 efectivas (el calentamiento no cuenta, mismo criterio que la comparacion).
const s = (orden: number, calentamiento = false) => ({
  id: `sr${orden}${calentamiento}`, ordenSerie: orden, pesoLevantadoKg: 40, repeticiones: 10, rir: 2,
  esCalentamiento: calentamiento, tiempoDescansoSegundos: null,
});
const CON_SERIES: Sesion = {
  ...SESION,
  ejercicios: [
    { id: 'e1', ejercicioId: 'ej-press', orden: 1, observaciones: null, seriesRealizadas: [s(1, true), s(2), s(3)] },
    { id: 'e2', ejercicioId: 'ej-sentadilla', orden: 2, observaciones: null, seriesRealizadas: [s(1)] },
    { id: 'e3', ejercicioId: 'ej-press', orden: 3, observaciones: null, seriesRealizadas: [s(1)] },
  ],
};

describe('MiHistorial (Socio): conteos del listado', () => {
  it('muestra ejercicios distintos y series efectivas registradas por sesion', async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: SesionesApi, useValue: { listarPorSocio: () => of([CON_SERIES]) } },
        { provide: AuthService, useValue: { sesion: () => ({ socioId: 's1' }) } },
      ],
    });
    const fixture = TestBed.createComponent(MiHistorial);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const meta = ((fixture.nativeElement as HTMLElement).querySelector('.item .meta')?.textContent ?? '').replace(/\s+/g, ' ');
    expect(meta).toContain('2 ejercicios · 4 series');
  });
});

// Etapa 8d: dos indicadores independientes por ejercicio, sin veredicto global. Datos sinteticos; el primer caso
// reproduce el de la Prensa de piernas del 25/11/2026 (objetivo 3 x 8-12; 58x8, 59x8, 75x5).
type SerieDato = [peso: number, reps: number, calentamiento?: boolean];
const PRENSA = { seriesObjetivo: 3, repeticionesMinimas: 8, repeticionesMaximas: 12, rirObjetivo: 2, descansoObjetivoSegundos: 90 };

function ejercicioComparado(series: SerieDato[] | null, tipoMedida: 'Repeticiones' | 'Segundos' = 'Repeticiones') {
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
    comparacion: { series: { prescritas: 3, realizadasEfectivas: efectivas, diferencia: efectivas - 3, mensaje: 'Cumple objetivo' } },
    ejecucion: series === null ? null : {
      cantidadSeriesTotales: detalles.length, cantidadSeriesEfectivas: efectivas, cantidadSeriesCalentamiento: detalles.length - efectivas,
      volumenTotalKg: 0, volumenEfectivoKg: 0, volumenCalentamientoKg: 0, detallesSeries: detalles,
    },
  };
}

async function textoDetalle(series: SerieDato[] | null, tipoMedida: 'Repeticiones' | 'Segundos' = 'Repeticiones') {
  const comparacion: ComparacionSesion = { sesionId: 'ses1', prescripcionEntrenamientoId: 'p1', ejercicios: [ejercicioComparado(series, tipoMedida)] };
  TestBed.configureTestingModule({
    providers: [provideRouter([]),
      { provide: SesionesApi, useValue: { obtener: () => of(SESION), comparacion: () => of(comparacion) } },
      { provide: EjerciciosApi, useValue: { nombres: () => of({ 'ej-prensa': 'Prensa de piernas' }) } }],
  });
  const fixture = TestBed.createComponent(MiSesionDetalle);
  fixture.componentRef.setInput('id', 'ses1');
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
  return ((fixture.nativeElement as HTMLElement).textContent ?? '').replace(/\s+/g, ' ');
}

describe('MiSesionDetalle (Socio): indicadores de cumplimiento (Etapa 8d)', () => {
  it('3 de 3 series con 8, 8 y 5 (rango 8-12): dos de tres en rango y sin "Cumple objetivo"', async () => {
    const t = await textoDetalle([[58, 8], [59, 8], [75, 5]]);
    expect(t).toContain('Series: 3 de 3');
    expect(t).toContain('Repeticiones en rango: 2 de 3 series');
    expect(t).not.toMatch(/cumple objetivo/i);
  });

  it('4 series con objetivo 3: "Series: 4 de 3", sin marcarlo como incumplimiento', async () => {
    const t = await textoDetalle([[50, 10], [50, 10], [50, 9], [50, 8]]);
    expect(t).toContain('Series: 4 de 3');
    expect(t).toContain('Repeticiones en rango: 4 de 4 series');
    expect(t).not.toMatch(/excedieron|no cumple|cumple objetivo/i);
  });

  it('2 series con objetivo 3: "Series: 2 de 3"', async () => {
    const t = await textoDetalle([[50, 10], [50, 9]]);
    expect(t).toContain('Series: 2 de 3');
    expect(t).toContain('Repeticiones en rango: 2 de 2 series');
    expect(t).not.toMatch(/faltaron|cumple objetivo/i);
  });

  it('ejercicio sin series registradas: "Series: 0 de 3" y sin indicador de repeticiones', async () => {
    const t = await textoDetalle(null);
    expect(t).toContain('Series: 0 de 3');
    expect(t).not.toContain('en rango');
    expect(t).toContain('No registraste series de este ejercicio.');
  });

  it('las series de calentamiento no cuentan en ninguno de los dos indicadores', async () => {
    // Calentamiento 30 x 15 (fuera de rango) y 20 x 10 (dentro): ninguno debe sumar.
    const t = await textoDetalle([[30, 15, true], [20, 10, true], [60, 9], [60, 6]]);
    expect(t).toContain('Series: 2 de 3');
    expect(t).toContain('Repeticiones en rango: 1 de 2 series');
  });

  it('en ejercicios por tiempo el indicador dice "Segundos en rango"', async () => {
    const t = await textoDetalle([[0, 10], [0, 30]], 'Segundos');
    expect(t).toContain('Segundos en rango: 1 de 2 series');
  });
});
