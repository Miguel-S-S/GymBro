import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { AlertasApi } from '../../core/api/alertas.api';
import { AlertaSeguridad } from '../../core/models/alertas.models';
import { MisAvisos } from './mis-avisos';

// Etapa 10a. Datos sintéticos de prueba.
function alerta(p: Partial<AlertaSeguridad>): AlertaSeguridad {
  return {
    id: 'a', socioId: 's1', nombreSocio: 'Ana', reglaCodigo: 'R-RIR-1', tipo: 'Alerta', severidad: 'Advertencia',
    mensaje: 'Press: llegaste mucho más cerca del fallo que lo planificado. La próxima vez dejá más margen.',
    estado: 'Pendiente', fechaAlta: '2026-11-19T10:00:00Z', vistaEn: null, sesionId: 'x', sesionEjercicioId: 'y',
    registroBienestarId: null, ejercicio: 'Press', cargaSugeridaKg: null, detalle: {}, ...p,
  };
}

const ALERTAS = [
  alerta({ id: 'rir' }),
  alerta({ id: 'prog', reglaCodigo: 'R-PROG-1', tipo: 'Sugerencia', severidad: 'Informativa', mensaje: 'Sentadilla: próxima vez, 42,5 kg.',
    ejercicio: 'Sentadilla', cargaSugeridaKg: 42.5 }),
  alerta({ id: 'fc', reglaCodigo: 'R-FC-1', mensaje: 'FC de reposo más alta que tu referencia (+16,7 %); considerá bajar la intensidad hoy.', ejercicio: null }),
];

function crear(lista$ = of(ALERTAS)) {
  const api = { listar: vi.fn(() => lista$), marcarVista: vi.fn(() => of({})) };
  TestBed.configureTestingModule({ providers: [{ provide: AlertasApi, useValue: api }] });
  const fixture = TestBed.createComponent(MisAvisos);
  fixture.detectChanges();
  return { fixture, api, el: fixture.nativeElement as HTMLElement };
}

describe('Avisos del Socio (Etapa 10a)', () => {
  it('pide solo las pendientes del propio socio', () => {
    const { api } = crear();
    expect(api.listar).toHaveBeenCalledWith(null, 'Pendiente');
  });

  it('muestra los avisos en lenguaje simple y la sugerencia de carga como "Próxima vez"', () => {
    const { el } = crear();
    expect(el.querySelector('li[data-regla="R-RIR-1"]')!.textContent).toContain('llegaste mucho más cerca del fallo');
    expect(el.querySelector('li[data-regla="R-PROG-1"]')!.textContent).toContain('Sentadilla · Próxima vez: 42,5 kg');
    expect(el.querySelector('li[data-regla="R-FC-1"]')!.textContent).toContain('FC de reposo más alta que tu referencia');
    expect(el.textContent).toContain('no cambian tu rutina');
    expect(el.textContent).not.toContain('RIR');   // sin jerga técnica para el socio
  });

  it('"Entendido" la marca vista y la quita de la lista', () => {
    const { fixture, api, el } = crear();
    (el.querySelector('li[data-regla="R-FC-1"] button.entendido') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(api.marcarVista).toHaveBeenCalledWith('fc');
    expect(el.querySelector('li[data-regla="R-FC-1"]')).toBeNull();
  });

  it('sin avisos, o si la consulta falla, la tarjeta no aparece', () => {
    expect(crear(of([])).el.querySelector('.tarjeta-avisos')).toBeNull();
    TestBed.resetTestingModule();
    expect(crear(throwError(() => new Error('x'))).el.querySelector('.tarjeta-avisos')).toBeNull();
  });
});
