import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { AccesoApi } from '../../core/api/acceso.api';
import { EquipamientoApi } from '../../core/api/equipamiento.api';
import { Rol, Sesion } from '../../core/auth/auth.models';
import { EquipamientoGimnasio } from '../../core/models/ejercicio.models';
import { Equipamiento } from './equipamiento';

// Etapa 9a-2. Datos sintéticos de prueba.
function datos(editable: boolean): EquipamientoGimnasio {
  return {
    gimnasioId: 'g1', nombreGimnasio: 'Gimnasio de prueba', editable,
    items: [
      { tipoEquipamientoId: 't-pc', clave: 'PESO_CORPORAL', nombre: 'Peso corporal', siempreDisponible: true, propio: false, marcado: true, cantidad: null },
      { tipoEquipamientoId: 't-m', clave: 'MANCUERNA', nombre: 'Mancuernas', siempreDisponible: false, propio: false, marcado: true, cantidad: 10 },
      { tipoEquipamientoId: 't-b', clave: 'BARRA', nombre: 'Barra', siempreDisponible: false, propio: false, marcado: false, cantidad: null },
      { tipoEquipamientoId: 't-k', clave: 'GIM_KETTLEBELL', nombre: 'Kettlebell', siempreDisponible: false, propio: true, marcado: false, cantidad: null },
    ],
  };
}

function guardarSesion(rol: Rol): void {
  const s: Sesion = { token: 'tok', rol, nombreCompleto: 'T', gimnasioId: null, socioId: null,
    expiraUtc: new Date(Date.now() + 30 * 60_000).toISOString() };
  localStorage.setItem('gymbro.sesion', JSON.stringify(s));
}

function crear(rol: Rol, editable: boolean) {
  guardarSesion(rol);
  const api = {
    obtener: vi.fn(() => of(datos(editable))),
    guardar: vi.fn(() => of(datos(editable))),
    crearTipo: vi.fn(),
  };
  const acceso = { listarGimnasios: vi.fn(() => of([{ id: 'g1', nombre: 'Gimnasio de prueba', codigo: 'X', requiereAprobacionRutinas: true, activo: true, fechaAlta: '' }])) };
  TestBed.configureTestingModule({
    providers: [{ provide: EquipamientoApi, useValue: api }, { provide: AccesoApi, useValue: acceso }],
  });
  const fixture = TestBed.createComponent(Equipamiento);
  fixture.detectChanges();
  return { fixture, api, acceso, el: fixture.nativeElement as HTMLElement };
}

const toggle = (el: HTMLElement, clave: string) =>
  el.querySelector(`li[data-clave="${clave}"] button[role="switch"]`) as HTMLButtonElement;

describe('Equipamiento (Etapa 9a-2)', () => {
  beforeEach(() => localStorage.clear());

  it('el Dueño edita: los siempre disponibles quedan fijos y Guardar envía solo los marcados', () => {
    const { fixture, api, el } = crear('AdministradorGimnasio', true);

    expect(api.obtener).toHaveBeenCalledWith(null);
    expect(toggle(el, 'PESO_CORPORAL').disabled).toBe(true);
    expect(el.querySelector('li[data-clave="PESO_CORPORAL"]')!.textContent).toContain('Incluido siempre');
    expect(el.querySelector('li[data-clave="GIM_KETTLEBELL"]')!.textContent).toContain('Propio');
    expect(toggle(el, 'BARRA').disabled).toBe(false);

    toggle(el, 'BARRA').click();
    fixture.detectChanges();
    (Array.from(el.querySelectorAll('button')).find((b) => b.textContent?.trim() === 'Guardar') as HTMLButtonElement).click();

    expect(api.guardar).toHaveBeenCalledWith([
      { tipoEquipamientoId: 't-m', cantidad: 10 },
      { tipoEquipamientoId: 't-b', cantidad: null },
    ]);
  });

  it('el Personal Trainer lo ve en solo lectura, sin Guardar ni alta de tipos', () => {
    const { el } = crear('Entrenador', false);

    expect(el.textContent).toContain('Solo lectura');
    expect(toggle(el, 'BARRA').disabled).toBe(true);
    expect(Array.from(el.querySelectorAll('button')).some((b) => b.textContent?.trim() === 'Guardar')).toBe(false);
    expect(el.querySelector('form.nuevo')).toBeNull();
  });

  it('el Administrador de la Plataforma abre la pantalla, elige el gimnasio y la ve en solo lectura', () => {
    const { fixture, api, acceso, el } = crear('SuperAdministrador', false);

    expect(acceso.listarGimnasios).toHaveBeenCalled();
    expect(api.obtener).not.toHaveBeenCalled();
    expect(el.querySelector('mat-select')).not.toBeNull();
    expect(el.textContent).toContain('Elegí un gimnasio');

    fixture.componentInstance.cargar('g1');
    fixture.detectChanges();

    expect(api.obtener).toHaveBeenCalledWith('g1');
    expect(el.textContent).toContain('Solo lectura');
    expect(Array.from(el.querySelectorAll('button')).some((b) => b.textContent?.trim() === 'Guardar')).toBe(false);
  });
});
