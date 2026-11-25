import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RutinaPendiente } from '../../core/models/acceso.models';
import { RegistrarSesionRequest } from '../../core/models/sesion.models';
import { leerBorrador } from '../../core/util/borrador-sesion';
import { Entrenamiento } from './entrenamiento';
import { MiEntrenar } from './mi-entrenar';

// Datos sintéticos de prueba.
const RUTINA: RutinaPendiente = {
  id: 'p1', socioId: 's1', nombreSocio: 'Ana', origenGeneracion: 'BASELINE_DETERMINISTA', observaciones: null,
  estado: 'Aprobada', fechaAlta: '2026-11-10T10:00:00Z',
  ejercicios: [{ id: 'pe1', ejercicioId: 'ej-press', orden: 1, ejercicio: 'Press', seriesObjetivo: 3, repeticionesMinimas: 8,
    repeticionesMaximas: 12, rirObjetivo: 2, descansoObjetivoSegundos: 90, observaciones: null }],
};

const valor = (v: string) => ({ target: { value: v } }) as unknown as Event;

describe('MiEntrenar (pantalla Entrenar)', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    localStorage.setItem('gymbro.sesion', JSON.stringify({
      token: 't', rol: 'Socio', nombreCompleto: 'Ana', gimnasioId: null, socioId: 's1',
      expiraUtc: new Date(Date.now() + 3_600_000).toISOString(),
    }));
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(),
        provideRouter([{ path: 'mi', children: [] }, { path: 'mi/historial/:id', children: [] }, { path: 'login', children: [] }])],
    });
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => { http.verify(); localStorage.clear(); });

  async function abrir() {
    const fixture = TestBed.createComponent(MiEntrenar);
    http.expectOne('/api/Socio/s1/prescripciones').flush([RUTINA]);
    http.expectOne((r) => r.method === 'GET' && r.url === '/api/Sesiones').flush([]);
    fixture.detectChanges();
    await fixture.whenStable();
    return fixture;
  }

  async function marcarPrimeraSerie(fixture: Awaited<ReturnType<typeof abrir>>) {
    const c = fixture.componentInstance;
    c.cambiar(0, 0, 'pesoKg', valor('40'));
    c.cambiar(0, 0, 'repeticiones', valor('10'));
    c.marcar(0, 0, c.estado()!.ejercicios[0].series[0]);
    fixture.detectChanges();
    await fixture.whenStable();
  }

  it('recargar la página conserva lo cargado (borrador local)', async () => {
    const primera = await abrir();
    await marcarPrimeraSerie(primera);
    expect(leerBorrador<Entrenamiento>('s1')?.ejercicios[0].series[0].hecha).toBe(true);
    primera.destroy();

    // "Recarga": un componente nuevo lee el borrador en lugar de empezar de cero.
    const segunda = await abrir();
    const serie = segunda.componentInstance.estado()!.ejercicios[0].series[0];
    expect(serie).toMatchObject({ pesoKg: 40, repeticiones: 10, hecha: true });
    expect(segunda.componentInstance.restante()).toBeGreaterThan(0); // el descanso sigue corriendo
    segunda.destroy();
  });

  it('Finalizar envía solo las series con ✔, vinculadas a la prescripción, y borra el borrador', async () => {
    const fixture = await abrir();
    await marcarPrimeraSerie(fixture);

    fixture.componentInstance.finalizar();
    const req = http.expectOne((r) => r.method === 'POST' && r.url === '/api/Sesiones');
    const body = req.request.body as RegistrarSesionRequest;
    expect(body.prescripcionEntrenamientoId).toBe('p1');
    expect(body.ejercicios[0].seriesRealizadas).toHaveLength(1); // 3 series objetivo, 1 con ✔
    expect(body.ejercicios[0].seriesRealizadas[0]).toMatchObject({ pesoLevantadoKg: 40, repeticiones: 10, ordenSerie: 1 });
    req.flush({ id: 'ses-1' });
    await fixture.whenStable();

    expect(leerBorrador('s1')).toBeNull();
    fixture.destroy();
  });

  it('cerrar sesión borra el borrador del dispositivo', async () => {
    const fixture = await abrir();
    await marcarPrimeraSerie(fixture);
    fixture.destroy();

    const { AuthService } = await import('../../core/auth/auth.service');
    TestBed.inject(AuthService).cerrarSesion();
    expect(leerBorrador('s1')).toBeNull();
  });
});

// Etapa 9a-1: la plancha se carga en segundos y el crunch en repeticiones (datos sintéticos).
describe('MiEntrenar: tipo de medida', () => {
  let http: HttpTestingController;
  const RUTINA_CORE: RutinaPendiente = {
    ...RUTINA,
    ejercicios: [
      { id: 'pe1', ejercicioId: 'ej-plancha', orden: 1, ejercicio: 'Plancha', seriesObjetivo: 2, repeticionesMinimas: 20,
        repeticionesMaximas: 40, tipoMedida: 'Segundos', rirObjetivo: 2, descansoObjetivoSegundos: 60, observaciones: null },
      { id: 'pe2', ejercicioId: 'ej-crunch', orden: 2, ejercicio: 'Crunch abdominal', seriesObjetivo: 3, repeticionesMinimas: 10,
        repeticionesMaximas: 15, tipoMedida: 'Repeticiones', rirObjetivo: 2, descansoObjetivoSegundos: 60, observaciones: null },
    ],
  };

  beforeEach(() => {
    localStorage.clear();
    localStorage.setItem('gymbro.sesion', JSON.stringify({
      token: 't', rol: 'Socio', nombreCompleto: 'Ana', gimnasioId: null, socioId: 's1',
      expiraUtc: new Date(Date.now() + 3_600_000).toISOString(),
    }));
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => { http.verify(); localStorage.clear(); });

  it('muestra segundos para la plancha y repeticiones para el crunch', async () => {
    const fixture = TestBed.createComponent(MiEntrenar);
    http.expectOne('/api/Socio/s1/prescripciones').flush([RUTINA_CORE]);
    http.expectOne((r) => r.method === 'GET' && r.url === '/api/Sesiones').flush([]);
    fixture.detectChanges();
    await fixture.whenStable();

    const [plancha, crunch] = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('section.ejercicio'));
    expect(plancha.textContent).toContain('2 series × 20–40 s');
    expect(plancha.querySelector('.encabezado-fila')!.textContent).toContain('Seg.');
    expect(plancha.querySelector('input[aria-label="Segundos serie 1"]')).not.toBeNull();

    expect(crunch.textContent).toContain('3 series × 10–15 reps');
    expect(crunch.querySelector('.encabezado-fila')!.textContent).toContain('Reps');
    expect(crunch.querySelector('input[aria-label="Repeticiones serie 1"]')).not.toBeNull();
    fixture.destroy();
  });
});

// Etapa 8d: autocompletado de "ultimas series" (columna "Anterior" y precarga), que depende del listado
// GET /api/Sesiones corregido en la Etapa 8 (antes llegaba sin ejercicios). Datos sinteticos.
describe('MiEntrenar: autocompletado con la sesion anterior', () => {
  let http: HttpTestingController;

  const SESION_PREVIA = {
    id: 'ses-prev', socioId: 's1', prescripcionEntrenamientoId: 'p1', inicio: '2026-11-20T18:00:00Z', fin: '2026-11-20T19:00:00Z',
    caloriasQuemadas: 0, frecuenciaCardiacaMaxima: 0, frecuenciaCardiacaPromedio: 0,
    ejercicios: [{
      id: 'se1', ejercicioId: 'ej-press', orden: 1, observaciones: null, prescripcionEjercicioId: 'pe1',
      seriesRealizadas: [
        { id: 'a', ordenSerie: 1, pesoLevantadoKg: 20, repeticiones: 15, rir: null, esCalentamiento: true, tiempoDescansoSegundos: null },
        { id: 'b', ordenSerie: 2, pesoLevantadoKg: 40, repeticiones: 10, rir: 2, esCalentamiento: false, tiempoDescansoSegundos: 90 },
        { id: 'c', ordenSerie: 3, pesoLevantadoKg: 42.5, repeticiones: 9, rir: 1, esCalentamiento: false, tiempoDescansoSegundos: 90 },
      ],
    }],
  };

  beforeEach(() => {
    localStorage.clear();
    localStorage.setItem('gymbro.sesion', JSON.stringify({
      token: 't', rol: 'Socio', nombreCompleto: 'Ana', gimnasioId: null, socioId: 's1',
      expiraUtc: new Date(Date.now() + 3_600_000).toISOString(),
    }));
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => { http.verify(); localStorage.clear(); });

  async function abrirCon(historial: unknown[]) {
    const fixture = TestBed.createComponent(MiEntrenar);
    http.expectOne('/api/Socio/s1/prescripciones').flush([RUTINA]);
    http.expectOne((r) => r.method === 'GET' && r.url === '/api/Sesiones').flush(historial);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    const filas = Array.from(el.querySelectorAll('section.ejercicio .fila:not(.encabezado-fila)'));
    const fila = (j: number) => ({
      anterior: filas[j].querySelector('.anterior')!.textContent!.trim(),
      peso: (filas[j].querySelector(`input[aria-label="Peso serie ${j + 1}"]`) as HTMLInputElement).value,
      reps: (filas[j].querySelector(`input[aria-label="Repeticiones serie ${j + 1}"]`) as HTMLInputElement).value,
    });
    return { fixture, fila };
  }

  it('con sesion previa: "Anterior" y la precarga traen peso y repeticiones de las series efectivas', async () => {
    const { fixture, fila } = await abrirCon([SESION_PREVIA]);
    expect(fila(0)).toEqual({ anterior: '40 kg × 10', peso: '40', reps: '10' }); // el calentamiento no cuenta
    expect(fila(1)).toEqual({ anterior: '42.5 kg × 9', peso: '42.5', reps: '9' });
    // La rutina pide 3 series y la vez anterior hubo 2 efectivas: la tercera queda sin peso y con el minimo.
    expect(fila(2)).toEqual({ anterior: '–', peso: '', reps: '8' });
    fixture.destroy();
  });

  it('sin sesion previa: peso vacio y el minimo del rango de repeticiones', async () => {
    const { fixture, fila } = await abrirCon([]);
    for (const j of [0, 1, 2]) expect(fila(j)).toEqual({ anterior: '–', peso: '', reps: '8' });
    fixture.destroy();
  });
});
