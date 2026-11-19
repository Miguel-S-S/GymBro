import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { of } from 'rxjs';
import { EjerciciosApi } from '../../core/api/ejercicios.api';
import { Rol, Sesion } from '../../core/auth/auth.models';
import { EjercicioResumen } from '../../core/models/ejercicio.models';
import { EjerciciosCatalogo } from './ejercicios-catalogo';
import { EjercicioFormDialog } from './ejercicio-form-dialog';

// Etapa 9a-2. Datos sintéticos de prueba: un ejercicio de cada origen.
const ITEMS: EjercicioResumen[] = [
  { id: 'e-d', nombre: 'Press de banca', zonaObjetivo: 'Chest', musculoPrincipal: 'Pectoral', equipamiento: 'Barbell', dificultad: 2, origen: 'Dataset' },
  { id: 'e-p', nombre: 'Plancha', zonaObjetivo: 'Core', musculoPrincipal: 'Recto abdominal', equipamiento: 'Body Weight', dificultad: 2, origen: 'Proyecto' },
  { id: 'e-g', nombre: 'Plancha con toalla', zonaObjetivo: 'Core', musculoPrincipal: 'Recto abdominal', equipamiento: 'Peso corporal',
    dificultad: 2, origen: 'Gimnasio', gimnasioId: 'g1' },
];

function guardarSesion(rol: Rol): void {
  const s: Sesion = { token: 'tok', rol, nombreCompleto: 'T', gimnasioId: null, socioId: null,
    expiraUtc: new Date(Date.now() + 30 * 60_000).toISOString() };
  localStorage.setItem('gymbro.sesion', JSON.stringify(s));
}

function crear(rol: Rol) {
  guardarSesion(rol);
  const api = {
    zonas: () => of(['Chest', 'Core']),
    catalogo: vi.fn(() => of({ items: ITEMS, total: ITEMS.length, pagina: 1, tamanioPagina: 20 })),
  };
  const dialogo = { open: vi.fn(() => ({ afterClosed: () => of(undefined) })) };
  TestBed.configureTestingModule({
    providers: [{ provide: EjerciciosApi, useValue: api }, { provide: MatDialog, useValue: dialogo }],
  });
  const fixture = TestBed.createComponent(EjerciciosCatalogo);
  fixture.detectChanges();
  return { fixture, api, dialogo, el: fixture.nativeElement as HTMLElement };
}

const filas = (el: HTMLElement) => Array.from(el.querySelectorAll('tr.mat-mdc-row'));
const botonNuevo = (el: HTMLElement) => Array.from(el.querySelectorAll('button')).find((b) => b.textContent?.includes('Nuevo ejercicio'));

describe('Catálogo de ejercicios por rol (Etapa 9a-2)', () => {
  beforeEach(() => localStorage.clear());

  it('muestra la insignia de origen de cada ejercicio', () => {
    const { el } = crear('Entrenador');
    expect(filas(el).map((f) => f.querySelector('.insignia')?.textContent?.trim())).toEqual(['Dataset', 'Proyecto', 'Gimnasio']);
  });

  it('el Dueño ve "Nuevo ejercicio" y editar/dar de baja solo en los propios', () => {
    const { el, dialogo } = crear('AdministradorGimnasio');

    const [dataset, proyecto, propio] = filas(el);
    expect(dataset.querySelector('button.editar')).toBeNull();
    expect(proyecto.querySelector('button.baja')).toBeNull();
    expect(propio.querySelector('button.editar')).not.toBeNull();
    expect(propio.querySelector('button.baja')).not.toBeNull();

    botonNuevo(el)!.click();
    expect(dialogo.open).toHaveBeenCalledWith(EjercicioFormDialog, expect.objectContaining({ data: {} }));
    (propio.querySelector('button.editar') as HTMLButtonElement).click();
    expect(dialogo.open).toHaveBeenCalledWith(EjercicioFormDialog, expect.objectContaining({ data: { id: 'e-g' } }));
  });

  it.each<Rol>(['Entrenador', 'SuperAdministrador'])('%s ve el catálogo completo, sin botones de escritura', (rol) => {
    const { el } = crear(rol);
    expect(filas(el)).toHaveLength(3);
    expect(botonNuevo(el)).toBeUndefined();
    expect(el.querySelector('button.editar, button.baja')).toBeNull();
  });

  it('el filtro de origen se envía a la API', () => {
    const { fixture, api } = crear('AdministradorGimnasio');
    fixture.componentInstance.origen.setValue('Gimnasio');
    expect(api.catalogo).toHaveBeenLastCalledWith(expect.objectContaining({ origen: 'Gimnasio', pagina: 1 }));
  });
});
