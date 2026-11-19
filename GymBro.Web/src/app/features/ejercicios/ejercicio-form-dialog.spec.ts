import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { of, throwError } from 'rxjs';
import { AccesoApi } from '../../core/api/acceso.api';
import { EjerciciosApi } from '../../core/api/ejercicios.api';
import { Rol, Sesion } from '../../core/auth/auth.models';
import { EquipamientoApi } from '../../core/api/equipamiento.api';
import { EquipamientoGimnasio, tiposElegibles } from '../../core/models/ejercicio.models';
import { EjercicioFormDialog } from './ejercicio-form-dialog';

// Etapa 9a-2. Datos sintéticos de prueba.
const EQUIPAMIENTO: EquipamientoGimnasio = {
  gimnasioId: 'g1', nombreGimnasio: 'Gimnasio de prueba', editable: true,
  items: [
    { tipoEquipamientoId: 't-pc', clave: 'PESO_CORPORAL', nombre: 'Peso corporal', siempreDisponible: true, propio: false, marcado: true, cantidad: null },
    { tipoEquipamientoId: 't-m', clave: 'MANCUERNA', nombre: 'Mancuernas', siempreDisponible: false, propio: false, marcado: true, cantidad: null },
    { tipoEquipamientoId: 't-b', clave: 'BARRA', nombre: 'Barra', siempreDisponible: false, propio: false, marcado: false, cantidad: null },
  ],
};

function guardarSesion(rol: Rol): void {
  const s: Sesion = { token: 'tok', rol, nombreCompleto: 'T', gimnasioId: null, socioId: null,
    expiraUtc: new Date(Date.now() + 30 * 60_000).toISOString() };
  localStorage.setItem('gymbro.sesion', JSON.stringify(s));
}

const GIMNASIOS = [{ id: 'g2', nombre: 'Gimnasio dos', codigo: 'X', requiereAprobacionRutinas: true, activo: true, fechaAlta: '' }];

function crear(crear$: unknown, rol: Rol = 'AdministradorGimnasio', data: { id?: string } = {}) {
  localStorage.clear();
  guardarSesion(rol);
  const api = {
    zonas: () => of(['Chest', 'Core']), crear: vi.fn(() => crear$), editar: vi.fn(() => of({ id: data.id })),
    obtener: vi.fn(() => of({
      id: 'e-g', nombre: 'Plancha con toalla', zonaObjetivo: 'Core', musculoPrincipal: 'Recto abdominal', equipamiento: 'Mancuernas',
      dificultad: 2, origen: 'Gimnasio', gimnasioId: 'g2', tipoEquipamientoId: 't-m', tipoEquipamiento: 'Mancuernas', sinergistas: '',
      estabilizadores: '', preparacion: '', ejecucion: 'Sostener.', creadoPor: 'Trainer de prueba',
    })),
  };
  const equipamiento = { obtener: vi.fn(() => of(EQUIPAMIENTO)) };
  const acceso = { listarGimnasios: vi.fn(() => of(GIMNASIOS)) };
  const ref = { close: vi.fn() };
  TestBed.configureTestingModule({
    providers: [
      { provide: EjerciciosApi, useValue: api },
      { provide: EquipamientoApi, useValue: equipamiento },
      { provide: AccesoApi, useValue: acceso },
      { provide: MatDialogRef, useValue: ref },
      { provide: MAT_DIALOG_DATA, useValue: data },
    ],
  });
  const fixture = TestBed.createComponent(EjercicioFormDialog);
  fixture.detectChanges();
  return { fixture, api, ref, equipamiento, acceso, c: fixture.componentInstance, el: fixture.nativeElement as HTMLElement };
}

function completar(c: EjercicioFormDialog): void {
  c.form.setValue({
    nombre: 'Plancha con toalla', zonaObjetivo: 'Core', tipoEquipamientoId: 't-m', musculoPrincipal: 'Recto abdominal',
    tipoMedida: 'Segundos', dificultad: 2, preparacion: '', ejecucion: 'Sostener la posición.',
  });
}

describe('Formulario de ejercicio propio (Etapas 9a-2 y 9a-2b)', () => {
  it('el equipamiento se limita a lo marcado y a lo siempre disponible', () => {
    expect(tiposElegibles(EQUIPAMIENTO).map((t) => t.clave)).toEqual(['PESO_CORPORAL', 'MANCUERNA']);
    const { c } = crear(of({}));
    expect(c.tipos().map((t) => t.tipoEquipamientoId)).toEqual(['t-pc', 't-m']);
    expect(c.zonas()).toEqual(['Chest', 'Core']);
  });

  it('no envía un formulario incompleto', () => {
    const { c, api } = crear(of({}));
    c.guardar();
    expect(api.crear).not.toHaveBeenCalled();
    expect(c.form.controls.nombre.touched).toBe(true);
  });

  it('envía el pedido y cierra el diálogo con el ejercicio creado', () => {
    const creado = { id: 'nuevo' };
    const { c, api, ref } = crear(of(creado));
    completar(c);
    c.guardar();
    expect(api.crear).toHaveBeenCalledWith(expect.objectContaining({ nombre: 'Plancha con toalla', tipoEquipamientoId: 't-m', preparacion: null }));
    expect(ref.close).toHaveBeenCalledWith(creado);
  });

  it('muestra el mensaje del servidor si rechaza el pedido', () => {
    const error = new HttpErrorResponse({ status: 400, error: { title: 'Solicitud inválida', detail: 'Ya existe un ejercicio con ese nombre.' } });
    const { fixture, c, ref } = crear(throwError(() => error));
    completar(c);
    c.guardar();
    fixture.detectChanges();
    expect(ref.close).not.toHaveBeenCalled();
    expect((fixture.nativeElement as HTMLElement).querySelector('[role="alert"]')!.textContent).toContain('Ya existe un ejercicio con ese nombre.');
  });

  it('muestra la advertencia de que no tiene validación profesional', () => {
    const { el } = crear(of({}));
    expect(el.querySelector('.advertencia')!.textContent).toContain('no pasan por una validación profesional');
  });

  it('el Dueño y el Personal Trainer no eligen gimnasio ni lo envían', () => {
    for (const rol of ['AdministradorGimnasio', 'Entrenador'] as Rol[]) {
      TestBed.resetTestingModule();
      const { c, api, acceso, el } = crear(of({ id: 'x' }), rol);
      expect(c.eligeGimnasio).toBe(false);
      expect(acceso.listarGimnasios).not.toHaveBeenCalled();
      expect(el.querySelector('mat-form-field.gimnasio')).toBeNull();
      completar(c);
      c.guardar();
      expect(api.crear).toHaveBeenCalledWith(expect.not.objectContaining({ gimnasioId: expect.anything() }));
    }
  });

  it('el Administrador de la Plataforma elige el gimnasio, ve sus tipos y lo envía en el alta (D2)', () => {
    const { c, api, equipamiento, acceso } = crear(of({ id: 'x' }), 'SuperAdministrador');
    expect(c.eligeGimnasio).toBe(true);
    expect(acceso.listarGimnasios).toHaveBeenCalled();
    expect(equipamiento.obtener).not.toHaveBeenCalled();

    // Sin gimnasio no se envía.
    completar(c);
    c.guardar();
    expect(api.crear).not.toHaveBeenCalled();

    c.gimnasio.setValue('g2');
    c.cargarTipos('g2');
    expect(equipamiento.obtener).toHaveBeenCalledWith('g2');
    completar(c);
    c.guardar();
    expect(api.crear).toHaveBeenCalledWith(expect.objectContaining({ gimnasioId: 'g2', nombre: 'Plancha con toalla' }));
  });

  it('al editar, la plataforma usa el equipamiento del gimnasio del ejercicio y se muestra el autor', () => {
    const { c, equipamiento, el } = crear(of({}), 'SuperAdministrador', { id: 'e-g' });
    expect(c.eligeGimnasio).toBe(false);
    expect(equipamiento.obtener).toHaveBeenCalledWith('g2');
    expect(c.form.controls.nombre.value).toBe('Plancha con toalla');
    expect(el.textContent).toContain('Propuesto por: Trainer de prueba');
  });
});
