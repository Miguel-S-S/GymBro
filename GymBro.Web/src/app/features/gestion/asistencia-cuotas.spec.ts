import { Type } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { AccesoApi } from '../../core/api/acceso.api';
import { AsistenciaApi } from '../../core/api/asistencia.api';
import { SociosApi } from '../../core/api/socios.api';
import { Rol, Sesion } from '../../core/auth/auth.models';
import { Asistencia, Cuota } from '../../core/models/asistencia.models';
import { MiAsistencia } from '../mi/mi-asistencia';
import { AsistenciaDia, detectorDisponible } from './asistencia';
import { Cuotas } from './cuotas';

// Etapa 11. Datos sinteticos de prueba.
function guardarSesion(rol: Rol): void {
  const s: Sesion = { token: 'tok', rol, nombreCompleto: 'T', gimnasioId: null, socioId: null,
    expiraUtc: new Date(Date.now() + 30 * 60_000).toISOString() };
  localStorage.setItem('gymbro.sesion', JSON.stringify(s));
}

const ASISTENCIA: Asistencia = {
  id: 'a1', socioId: 's1', socioNombre: 'Ana Sintetica', gimnasioId: 'g1', gimnasioNombre: 'Gimnasio Uno',
  instante: '2026-11-25T12:30:00Z', fecha: '2026-11-25', metodo: 'Qr', registradoPorUsuarioId: 'u1',
};

function cuota(p: Partial<Cuota>): Cuota {
  return {
    id: 'c1', socioId: 's1', socioNombre: 'Ana Sintetica', gimnasioId: 'g1', gimnasioNombre: 'Gimnasio Uno', anio: 2026, mes: 11,
    monto: 15000, vencimiento: '2026-11-10', fechaPago: null, estado: 'Pendiente', vencida: false, diasVencida: 0, ...p,
  };
}

function apiFalsa() {
  return {
    generarQr: vi.fn(() => of({ token: 'AEXVKZ5ZQAGGLM4M2KMK2Q7WIW3CSAAAAAAAAANTB2GL3YB6G2CPA7KZF2YQ',
      expiraUtc: new Date(Date.now() + 120_000).toISOString(), duracionSegundos: 120 })),
    misCuotas: vi.fn(() => of([cuota({ id: 'c1', vencida: true, diasVencida: 3 }), cuota({ id: 'c2', mes: 10, estado: 'Pagada', fechaPago: '2026-10-05' })])),
    delDia: vi.fn(() => of({ fecha: '2026-11-25', total: 1, asistencias: [ASISTENCIA] })),
    registrarPorQr: vi.fn(() => of({ yaRegistrada: false, asistencia: ASISTENCIA })),
    registrarManual: vi.fn(() => of({ yaRegistrada: true, asistencia: { ...ASISTENCIA, metodo: 'Manual' as const } })),
    cuotasDelPeriodo: vi.fn(() => of([cuota({ id: 'c1', vencida: true, diasVencida: 2 }), cuota({ id: 'c3', socioNombre: 'Bruno', estado: 'Pagada', fechaPago: '2026-11-02' })])),
    morosos: vi.fn(() => of([cuota({ id: 'c1', vencida: true, diasVencida: 2 })])),
    generarCuotas: vi.fn(() => of({ creadas: 2, existentes: 0 })),
    registrarPago: vi.fn(() => of(cuota({ id: 'c1', estado: 'Pagada', fechaPago: '2026-11-25' }))),
  };
}

async function montar<T>(componente: Type<T>, rol: Rol, api = apiFalsa()) {
  guardarSesion(rol);
  TestBed.configureTestingModule({
    providers: [
      { provide: AsistenciaApi, useValue: api },
      { provide: SociosApi, useValue: { listar: () => of([{ id: 's1', nombreCompleto: 'Ana Sintetica' }]) } },
      { provide: AccesoApi, useValue: { listarGimnasios: () => of([{ id: 'g1', nombre: 'Gimnasio Uno' }, { id: 'g2', nombre: 'Gimnasio Dos' }]) } }],
  });
  const fixture = TestBed.createComponent(componente);
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
  const texto = () => ((fixture.nativeElement as HTMLElement).textContent ?? '').replace(/\s+/g, ' ');
  return { fixture, api, texto, el: fixture.nativeElement as HTMLElement };
}

describe('MiAsistencia (Socio, Etapa 11)', () => {
  beforeEach(() => localStorage.clear());

  it('dibuja el QR en el cliente, muestra el codigo en bloques y el estado derivado de las cuotas', async () => {
    const { el, texto, fixture } = await montar(MiAsistencia, 'Socio');
    expect(el.querySelector('svg.codigo path')!.getAttribute('d')!.length).toBeGreaterThan(100);
    expect(texto()).toContain('AEXV-KZ5Z-QAGG');
    expect(texto()).toMatch(/Vence en \d+ s/);
    expect(texto()).toContain('Vencida hace 3 días');
    expect(texto()).toContain('Pagada el 05/10/2026');
    fixture.destroy();
  });
});

describe('AsistenciaDia (personal y plataforma, Etapa 11)', () => {
  beforeEach(() => localStorage.clear());

  it('sin BarcodeDetector no ofrece la camara y siempre ofrece el codigo tipeado', async () => {
    expect(detectorDisponible({})).toBe(false);
    expect(detectorDisponible({ BarcodeDetector: class {} })).toBe(true);
    const { texto, el, fixture } = await montar(AsistenciaDia, 'Entrenador');
    if (!detectorDisponible()) expect(texto()).toContain('no permite leer el QR con la cámara');
    expect(el.querySelector('input[autocapitalize]')).not.toBeNull();
    expect(texto()).toContain('1 ingreso');
    fixture.destroy();
  });

  it('el personal valida un codigo tipeado y la lista se recarga', async () => {
    const { fixture, api, texto } = await montar(AsistenciaDia, 'AdministradorGimnasio');
    fixture.componentInstance.codigo.setValue(' aexv-kz5z ');
    fixture.componentInstance.registrarCodigo();
    fixture.detectChanges();
    expect(api.registrarPorQr).toHaveBeenCalledWith('aexv-kz5z');
    expect(texto()).toContain('Ana Sintetica: ingreso registrado (QR)');
    expect(api.delDia).toHaveBeenCalledTimes(2);
    fixture.destroy();
  });

  it('un registro repetido del dia se informa como ya registrado', async () => {
    const { fixture, texto } = await montar(AsistenciaDia, 'Entrenador');
    fixture.componentInstance.socioElegido.setValue('s1');
    fixture.componentInstance.registrarManual();
    fixture.detectChanges();
    expect(texto()).toContain('ya tenía registrado el ingreso de hoy (manual)');
    fixture.destroy();
  });

  it('la plataforma ve el selector de gimnasios (con "Todos") y no ve el registro', async () => {
    const { texto, api, fixture } = await montar(AsistenciaDia, 'SuperAdministrador');
    expect(texto()).toContain('Solo lectura');
    expect(texto()).not.toContain('Registrar ingreso');
    expect(texto()).toContain('Gimnasio Uno');
    expect(api.delDia).toHaveBeenCalledWith(null, null);
    fixture.destroy();
  });
});

describe('Cuotas (Dueño y plataforma, Etapa 11)', () => {
  beforeEach(() => localStorage.clear());

  it('el Dueño ve pendientes y vencidas del mes y puede registrar el pago', async () => {
    const { texto, fixture, api, el } = await montar(Cuotas, 'AdministradorGimnasio');
    expect(texto()).toContain('2 cuotas · 1 pendientes · 1 vencidas');
    expect(texto()).toContain('Vencida (2 d)');
    expect(texto()).toContain('Generar cuotas de');
    const botones = Array.from(el.querySelectorAll('button')).filter((b) => b.textContent?.includes('Registrar pago'));
    expect(botones.length).toBe(1);
    fixture.componentInstance.registrarPago(fixture.componentInstance.cuotas()[0]);
    fixture.detectChanges();
    expect(api.registrarPago).toHaveBeenCalledWith('c1');
    expect(texto()).toContain('Pagada');
    fixture.destroy();
  });

  it('la vista de morosos consulta las cuotas vencidas', async () => {
    const { fixture, api } = await montar(Cuotas, 'AdministradorGimnasio');
    fixture.componentInstance.vista.set('morosos');
    fixture.componentInstance.cargar();
    expect(api.morosos).toHaveBeenCalledWith(null);
    fixture.destroy();
  });

  it('la plataforma lee sin generar ni registrar pagos', async () => {
    const { texto, el, fixture } = await montar(Cuotas, 'SuperAdministrador');
    expect(texto()).toContain('Solo lectura');
    expect(texto()).not.toContain('Generar cuotas de');
    expect(Array.from(el.querySelectorAll('button')).some((b) => b.textContent?.includes('Registrar pago'))).toBe(false);
    expect(texto()).toContain('Gimnasio Uno');
    fixture.destroy();
  });
});
