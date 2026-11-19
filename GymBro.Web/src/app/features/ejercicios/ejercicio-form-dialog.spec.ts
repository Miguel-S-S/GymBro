import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { of, throwError } from 'rxjs';
import { EjerciciosApi } from '../../core/api/ejercicios.api';
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

function crear(crear$: unknown) {
  const api = { zonas: () => of(['Chest', 'Core']), crear: vi.fn(() => crear$), editar: vi.fn(), obtener: vi.fn() };
  const ref = { close: vi.fn() };
  TestBed.configureTestingModule({
    providers: [
      { provide: EjerciciosApi, useValue: api },
      { provide: EquipamientoApi, useValue: { obtener: () => of(EQUIPAMIENTO) } },
      { provide: MatDialogRef, useValue: ref },
      { provide: MAT_DIALOG_DATA, useValue: {} },
    ],
  });
  const fixture = TestBed.createComponent(EjercicioFormDialog);
  fixture.detectChanges();
  return { fixture, api, ref, c: fixture.componentInstance };
}

function completar(c: EjercicioFormDialog): void {
  c.form.setValue({
    nombre: 'Plancha con toalla', zonaObjetivo: 'Core', tipoEquipamientoId: 't-m', musculoPrincipal: 'Recto abdominal',
    tipoMedida: 'Segundos', dificultad: 2, preparacion: '', ejecucion: 'Sostener la posición.',
  });
}

describe('Formulario de ejercicio propio (Etapa 9a-2)', () => {
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
});
