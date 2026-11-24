import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { RutinaPendiente } from '../../core/models/acceso.models';
import { RutinasPanel } from './rutinas-panel';

/** Etapa 7d: tarjeta "Protocolo de piernas" del panel de rutinas (datos sintéticos de prueba). */

const SOCIO = 's-1';

function rutina(id: string, extra: Partial<RutinaPendiente> = {}): RutinaPendiente {
  return {
    id, socioId: SOCIO, nombreSocio: 'Socio Sintetico', origenGeneracion: 'BASELINE_DETERMINISTA', observaciones: null,
    estado: 'Aprobada', fechaAlta: '2026-11-20T12:00:00Z', ejercicios: [], ...extra,
  };
}

describe('RutinasPanel: protocolo de piernas (Etapa 7d)', () => {
  let http: HttpTestingController;
  let mensajes: string[];

  beforeEach(() => {
    mensajes = [];
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(), provideHttpClientTesting(),
        { provide: MatSnackBar, useValue: { open: (m: string) => { mensajes.push(m); } } },
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  async function crear(rutinas: RutinaPendiente[] = [rutina('r-0')]) {
    const fixture = TestBed.createComponent(RutinasPanel);
    fixture.componentRef.setInput('socioId', SOCIO);
    fixture.detectChanges();
    http.expectOne(`/api/Socio/${SOCIO}/prescripciones`).flush(rutinas);
    http.expectOne(`/api/Socio/${SOCIO}/limitaciones-articulares`).flush({ rodilla: 'Moderada', lumbar: 'SinLimitacion' });
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture;
  }

  function boton(fixture: { nativeElement: HTMLElement }, texto: string): HTMLButtonElement {
    const b = Array.from(fixture.nativeElement.querySelectorAll('button')).find((x) => (x.textContent ?? '').includes(texto));
    if (!b) throw new Error(`No se encontró el botón ${texto}`);
    return b as HTMLButtonElement;
  }

  it('conserva la tarjeta baseline y agrega la de piernas con las limitaciones cargadas', async () => {
    const fixture = await crear();
    const texto = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(texto).toContain('Rutina baseline');
    expect(texto).toContain('Protocolo de piernas');
    expect(texto).toContain('Generar rutina de piernas');
    expect(fixture.componentInstance.rodilla()).toBe('Moderada');
    expect(fixture.componentInstance.lumbar()).toBe('SinLimitacion');
  });

  it('guarda las limitaciones seleccionadas con PUT', async () => {
    const fixture = await crear();
    fixture.componentInstance.lumbar.set('Alta');
    boton(fixture, 'Guardar limitaciones').click();

    const req = http.expectOne(`/api/Socio/${SOCIO}/limitaciones-articulares`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ rodilla: 'Moderada', lumbar: 'Alta' });
    req.flush({ rodilla: 'Moderada', lumbar: 'Alta' });
    expect(mensajes).toContain('Limitaciones guardadas.');
  });

  it('genera la rutina de piernas y la agrega arriba del historial sin recargar', async () => {
    const fixture = await crear();
    boton(fixture, 'Generar rutina de piernas').click();

    const req = http.expectOne(`/api/Socio/${SOCIO}/prescripciones/piernas`);
    expect(req.request.method).toBe('POST');
    req.flush(rutina('r-piernas', {
      origenGeneracion: 'PROTOCOLO_PIERNAS_DETERMINISTA', estado: 'PendienteRevision', protocoloVersion: 'piernas-1.0', numeroGeneracion: 1,
      ejercicios: [{
        id: 'l1', ejercicioId: 'e1', orden: 1, ejercicio: 'Prensa de piernas 45 grados', seriesObjetivo: 3, repeticionesMinimas: 8,
        repeticionesMaximas: 12, rirObjetivo: 2, descansoObjetivoSegundos: 150, observaciones: null, rol: 'Referencia',
        grupo: 'Cuadriceps', tempoExcentricoSeg: 3, cargaSugeridaKg: null,
      }],
    }));
    fixture.detectChanges();

    expect(fixture.componentInstance.rutinas().map((r) => r.id)).toEqual(['r-piernas', 'r-0']);
    const texto = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(texto).toContain('piernas-1.0, generación 1');
    expect(texto).toContain('Referencia · tempo excéntrico 3 s');
    expect(mensajes).toContain('Rutina de piernas generada: queda pendiente de revisión.');
  });

  it('muestra el motivo de la API si falta el consentimiento o la medición', async () => {
    const fixture = await crear();
    boton(fixture, 'Generar rutina de piernas').click();
    http.expectOne(`/api/Socio/${SOCIO}/prescripciones/piernas`).flush(
      { title: 'Solicitud inválida', detail: 'El socio necesita al menos una medición física para generar la rutina.' },
      { status: 400, statusText: 'Bad Request' },
    );
    expect(mensajes).toContain('El socio necesita al menos una medición física para generar la rutina.');
    expect(fixture.componentInstance.generandoPiernas()).toBe(false);
    expect(fixture.componentInstance.rutinas().length).toBe(1);
  });
});
