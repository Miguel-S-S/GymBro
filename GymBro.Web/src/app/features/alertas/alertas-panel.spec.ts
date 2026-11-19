import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { AlertasApi } from '../../core/api/alertas.api';
import { AlertaSeguridad } from '../../core/models/alertas.models';
import { AlertasPanel } from './alertas-panel';

// Etapa 10a. Datos sintéticos de prueba.
const BASE: AlertaSeguridad = {
  id: 'a1', socioId: 's1', nombreSocio: 'Ana', reglaCodigo: 'R-FC-1', tipo: 'Alerta', severidad: 'Advertencia',
  mensaje: 'FC de reposo más alta que tu referencia (+16,7 %); considerá bajar la intensidad hoy.', estado: 'Pendiente',
  fechaAlta: '2026-11-19T10:00:00Z', vistaEn: null, sesionId: null, sesionEjercicioId: null, registroBienestarId: 'r1',
  ejercicio: null, cargaSugeridaKg: null,
  detalle: { entradas: { fc_reposo_lpm: 70 }, resultado: { linea_base_lpm: 60, variacion_porcentaje: 16.7 } },
};
const PROG: AlertaSeguridad = {
  ...BASE, id: 'a2', reglaCodigo: 'R-PROG-1', tipo: 'Sugerencia', severidad: 'Informativa', estado: 'Vista',
  mensaje: 'Press: próxima vez, 42 kg (subir desde 41 kg).', ejercicio: 'Press', cargaSugeridaKg: 42,
  detalle: { resultado: { carga_actual_kg: 41, carga_sugerida_kg: 42, cambio_porcentaje: 2.4 } },
};

function crear() {
  const api = { listar: vi.fn(() => of([BASE, PROG])), marcarVista: vi.fn(() => of({ ...BASE, estado: 'Vista' })) };
  TestBed.configureTestingModule({ providers: [{ provide: AlertasApi, useValue: api }] });
  const fixture = TestBed.createComponent(AlertasPanel);
  fixture.componentRef.setInput('socioId', 's1');
  fixture.detectChanges();
  return { fixture, api, el: fixture.nativeElement as HTMLElement };
}

describe('Alertas en la ficha del socio (Etapa 10a)', () => {
  it('pide las alertas del socio y muestra la regla y los valores', () => {
    const { api, el } = crear();
    expect(api.listar).toHaveBeenCalledWith('s1');
    const fc = el.querySelector('li[data-regla="R-FC-1"]')!;
    expect(fc.textContent).toContain('FC de reposo elevada');
    expect(fc.textContent).toContain('FC de reposo 70 lpm · línea base 60 lpm (16.7 %)');
    expect(el.querySelector('li[data-regla="R-PROG-1"]')!.textContent).toContain('41 kg → 42 kg');
  });

  it('el personal marca vista una pendiente; las vistas no tienen el botón', () => {
    const { fixture, api, el } = crear();
    expect(el.querySelector('li[data-regla="R-PROG-1"] button.marcar-vista')).toBeNull();
    (el.querySelector('li[data-regla="R-FC-1"] button.marcar-vista') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(api.marcarVista).toHaveBeenCalledWith('a1');
    expect(el.querySelector('li[data-regla="R-FC-1"] button.marcar-vista')).toBeNull();
  });
});
