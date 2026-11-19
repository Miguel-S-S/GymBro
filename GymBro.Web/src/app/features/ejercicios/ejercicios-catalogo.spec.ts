import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { of } from 'rxjs';
import { EjerciciosApi } from '../../core/api/ejercicios.api';
import { Rol, Sesion } from '../../core/auth/auth.models';
import { EjercicioResumen } from '../../core/models/ejercicio.models';
import { ConfirmarDialog } from '../../core/ui/confirmar-dialog';
import { EjerciciosCatalogo } from './ejercicios-catalogo';
import { EjercicioFormDialog } from './ejercicio-form-dialog';

// Etapas 9a-2 y 9a-2b. Datos sintéticos de prueba: un ejercicio de cada origen; los permisos los informa el servidor.
type Permisos = Partial<Record<'e-d' | 'e-p' | 'e-g', { puedeEditar: boolean; puedeCambiarEstado: boolean }>>;

function items(p: Permisos = {}, activo = true): EjercicioResumen[] {
  const base: EjercicioResumen[] = [
    { id: 'e-d', nombre: 'Press de banca', zonaObjetivo: 'Chest', musculoPrincipal: 'Pectoral', equipamiento: 'Barbell', dificultad: 2, origen: 'Dataset' },
    { id: 'e-p', nombre: 'Plancha', zonaObjetivo: 'Core', musculoPrincipal: 'Recto abdominal', equipamiento: 'Body Weight', dificultad: 2, origen: 'Proyecto' },
    { id: 'e-g', nombre: 'Plancha con toalla', zonaObjetivo: 'Core', musculoPrincipal: 'Recto abdominal', equipamiento: 'Peso corporal',
      dificultad: 2, origen: 'Gimnasio', gimnasioId: 'g1', creadoPor: 'Trainer de prueba' },
  ];
  return base.map((e) => ({ ...e, activo, ...(p[e.id as keyof Permisos] ?? { puedeEditar: false, puedeCambiarEstado: false }) }));
}

function guardarSesion(rol: Rol): void {
  const s: Sesion = { token: 'tok', rol, nombreCompleto: 'T', gimnasioId: null, socioId: null,
    expiraUtc: new Date(Date.now() + 30 * 60_000).toISOString() };
  localStorage.setItem('gymbro.sesion', JSON.stringify(s));
}

function crear(rol: Rol, lista: EjercicioResumen[] = items(), confirmar = true) {
  guardarSesion(rol);
  const api = {
    zonas: () => of(['Chest', 'Core']),
    catalogo: vi.fn(() => of({ items: lista, total: lista.length, pagina: 1, tamanioPagina: 20 })),
    cambiarEstado: vi.fn(() => of({})),
  };
  const dialogo = { open: vi.fn(() => ({ afterClosed: () => of(confirmar) })) };
  TestBed.configureTestingModule({
    providers: [{ provide: EjerciciosApi, useValue: api }, { provide: MatDialog, useValue: dialogo }],
  });
  const fixture = TestBed.createComponent(EjerciciosCatalogo);
  fixture.detectChanges();
  return { fixture, api, dialogo, el: fixture.nativeElement as HTMLElement };
}

const filas = (el: HTMLElement) => Array.from(el.querySelectorAll('tr.mat-mdc-row'));
const botonNuevo = (el: HTMLElement) => Array.from(el.querySelectorAll('button')).find((b) => b.textContent?.includes('Nuevo ejercicio'));

describe('Catálogo de ejercicios por rol (Etapas 9a-2 y 9a-2b)', () => {
  beforeEach(() => localStorage.clear());

  it('muestra la insignia de origen de cada ejercicio', () => {
    const { el } = crear('Entrenador');
    expect(filas(el).map((f) => f.querySelector('.insignia')?.textContent?.trim())).toEqual(['Dataset', 'Proyecto', 'Gimnasio']);
  });

  it('el Dueño ve "Nuevo ejercicio" y editar/desactivar solo donde el servidor lo permite', () => {
    const { el, dialogo } = crear('AdministradorGimnasio', items({ 'e-g': { puedeEditar: true, puedeCambiarEstado: true } }));

    const [dataset, proyecto, propio] = filas(el);
    expect(dataset.querySelector('button.editar')).toBeNull();
    expect(proyecto.querySelector('button.desactivar')).toBeNull();
    expect(propio.querySelector('button.editar')).not.toBeNull();
    expect(propio.querySelector('button.desactivar')).not.toBeNull();
    expect(el.querySelector('button.baja')).toBeNull(); // la baja de la 9a-2 ya no existe

    botonNuevo(el)!.click();
    expect(dialogo.open).toHaveBeenCalledWith(EjercicioFormDialog, expect.objectContaining({ data: {} }));
    (propio.querySelector('button.editar') as HTMLButtonElement).click();
    expect(dialogo.open).toHaveBeenCalledWith(EjercicioFormDialog, expect.objectContaining({ data: { id: 'e-g' } }));
  });

  it('el Personal Trainer propone ejercicios (D4) y solo modifica los que el servidor marca como suyos', () => {
    const { el } = crear('Entrenador', items({ 'e-g': { puedeEditar: true, puedeCambiarEstado: true } }));
    expect(botonNuevo(el)).toBeDefined();
    const [dataset, , propio] = filas(el);
    expect(dataset.querySelector('button.editar, button.desactivar')).toBeNull();
    expect(propio.querySelector('button.editar')).not.toBeNull();
  });

  it('el Personal Trainer no ve botones en un ejercicio propio de otro autor', () => {
    const { el } = crear('Entrenador', items());
    expect(el.querySelector('button.editar, button.desactivar, button.reactivar')).toBeNull();
  });

  it('el Administrador de la Plataforma gestiona: crea, edita propios y desactiva globales (D2)', () => {
    const { el, api, dialogo } = crear('SuperAdministrador', items({
      'e-d': { puedeEditar: false, puedeCambiarEstado: true },
      'e-g': { puedeEditar: true, puedeCambiarEstado: true },
    }));
    expect(filas(el)).toHaveLength(3);
    expect(botonNuevo(el)).toBeDefined();
    const [dataset, , propio] = filas(el);
    expect(dataset.querySelector('button.editar')).toBeNull(); // los globales se editan por script
    expect(propio.querySelector('button.editar')).not.toBeNull();

    (dataset.querySelector('button.desactivar') as HTMLButtonElement).click();
    expect(dialogo.open).toHaveBeenCalledWith(ConfirmarDialog, expect.anything());
    expect(api.cambiarEstado).toHaveBeenCalledWith('e-d', false);
  });

  it('desactivar no se envía si se cancela la confirmación', () => {
    const { el, api } = crear('AdministradorGimnasio', items({ 'e-g': { puedeEditar: true, puedeCambiarEstado: true } }), false);
    (filas(el)[2].querySelector('button.desactivar') as HTMLButtonElement).click();
    expect(api.cambiarEstado).not.toHaveBeenCalled();
  });

  it('"Ver desactivados" pide los inactivos y permite reactivar sin confirmación', () => {
    const { fixture, api, dialogo } = crear('AdministradorGimnasio', items({ 'e-g': { puedeEditar: true, puedeCambiarEstado: true } }, false));
    fixture.componentInstance.verInactivos.setValue(true);
    expect(api.catalogo).toHaveBeenLastCalledWith(expect.objectContaining({ inactivos: true, pagina: 1 }));
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    const propio = filas(el)[2];
    expect(propio.textContent).toContain('Desactivado');
    (propio.querySelector('button.reactivar') as HTMLButtonElement).click();
    expect(api.cambiarEstado).toHaveBeenCalledWith('e-g', true);
    expect(dialogo.open).not.toHaveBeenCalledWith(ConfirmarDialog, expect.anything());
  });

  it('muestra "Propuesto por" y la advertencia de ejercicios sin validación profesional a quien gestiona', () => {
    const { el } = crear('AdministradorGimnasio');
    expect(filas(el)[2].textContent).toContain('Propuesto por: Trainer de prueba');
    expect(el.querySelector('.advertencia')!.textContent).toContain('no pasan por una validación profesional');
  });

  it('el Socio no ve botones, ni "Ver desactivados", ni la advertencia', () => {
    const { el } = crear('Socio');
    expect(botonNuevo(el)).toBeUndefined();
    expect(el.querySelector('button.editar, button.desactivar, button.reactivar')).toBeNull();
    expect(el.textContent).not.toContain('Ver desactivados');
    expect(el.querySelector('.advertencia')).toBeNull();
  });

  it('el filtro de origen se envía a la API', () => {
    const { fixture, api } = crear('AdministradorGimnasio');
    fixture.componentInstance.origen.setValue('Gimnasio');
    expect(api.catalogo).toHaveBeenLastCalledWith(expect.objectContaining({ origen: 'Gimnasio', pagina: 1 }));
  });
});
