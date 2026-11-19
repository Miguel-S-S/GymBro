import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { of } from 'rxjs';
import { AccesoApi } from '../../core/api/acceso.api';
import { EquipamientoApi } from '../../core/api/equipamiento.api';
import { Rol, Sesion } from '../../core/auth/auth.models';
import { EquipamientoGimnasio, EquipamientoPrevisualizacion } from '../../core/models/ejercicio.models';
import { ConfirmarDialog } from '../../core/ui/confirmar-dialog';
import { Equipamiento } from './equipamiento';

// Etapas 9a-2 y 9a-2b. Datos sintéticos de prueba.
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

const SIN_AFECTADOS: EquipamientoPrevisualizacion = { cantidad: 0, ejercicios: [], tiposQuitados: [] };
const CON_AFECTADOS: EquipamientoPrevisualizacion = { cantidad: 12, ejercicios: ['Curl propio', 'Remo propio'], tiposQuitados: ['Mancuernas'] };

function guardarSesion(rol: Rol): void {
  const s: Sesion = { token: 'tok', rol, nombreCompleto: 'T', gimnasioId: null, socioId: null,
    expiraUtc: new Date(Date.now() + 30 * 60_000).toISOString() };
  localStorage.setItem('gymbro.sesion', JSON.stringify(s));
}

function crear(rol: Rol, editable: boolean, previa = SIN_AFECTADOS, confirmar = true) {
  guardarSesion(rol);
  const api = {
    obtener: vi.fn(() => of(datos(editable))),
    guardar: vi.fn(() => of(datos(editable))),
    previsualizar: vi.fn(() => of(previa)),
    crearTipo: vi.fn(() => of({ tipoEquipamientoId: 't-n', clave: 'GIM_SOGA', nombre: 'Soga', siempreDisponible: false, propio: true, marcado: false, cantidad: null })),
    editarTipo: vi.fn((id: string, nombre: string) => of({ ...datos(editable).items.find((i) => i.tipoEquipamientoId === id)!, nombre })),
    darDeBajaTipo: vi.fn(() => of(undefined)),
  };
  const acceso = { listarGimnasios: vi.fn(() => of([{ id: 'g1', nombre: 'Gimnasio de prueba', codigo: 'X', requiereAprobacionRutinas: true, activo: true, fechaAlta: '' }])) };
  const dialogo = { open: vi.fn(() => ({ afterClosed: () => of(confirmar) })) };
  TestBed.configureTestingModule({
    providers: [{ provide: EquipamientoApi, useValue: api }, { provide: AccesoApi, useValue: acceso }, { provide: MatDialog, useValue: dialogo }],
  });
  const fixture = TestBed.createComponent(Equipamiento);
  fixture.detectChanges();
  return { fixture, api, acceso, dialogo, el: fixture.nativeElement as HTMLElement };
}

const toggle = (el: HTMLElement, clave: string) =>
  el.querySelector(`li[data-clave="${clave}"] button[role="switch"]`) as HTMLButtonElement;
const boton = (el: HTMLElement, texto: string) =>
  Array.from(el.querySelectorAll('button')).find((b) => b.textContent?.trim() === texto) as HTMLButtonElement | undefined;

describe('Equipamiento (Etapas 9a-2 y 9a-2b)', () => {
  beforeEach(() => localStorage.clear());

  it('el Dueño edita: los siempre disponibles quedan fijos y Guardar previsualiza y envía solo los marcados', () => {
    const { fixture, api, dialogo, el } = crear('AdministradorGimnasio', true);

    expect(api.obtener).toHaveBeenCalledWith(null);
    expect(toggle(el, 'PESO_CORPORAL').disabled).toBe(true);
    expect(el.querySelector('li[data-clave="PESO_CORPORAL"]')!.textContent).toContain('Incluido siempre');
    expect(el.querySelector('li[data-clave="GIM_KETTLEBELL"]')!.textContent).toContain('Propio');
    expect(toggle(el, 'BARRA').disabled).toBe(false);

    toggle(el, 'BARRA').click();
    fixture.detectChanges();
    boton(el, 'Guardar')!.click();

    const esperado = [{ tipoEquipamientoId: 't-m', cantidad: 10 }, { tipoEquipamientoId: 't-b', cantidad: null }];
    expect(api.previsualizar).toHaveBeenCalledWith(esperado, null);
    // Sin ejercicios afectados no se pide confirmación.
    expect(dialogo.open).not.toHaveBeenCalled();
    expect(api.guardar).toHaveBeenCalledWith(esperado, null);
  });

  it('si quitar equipamiento deja ejercicios propios afectados, avisa cuántos y cuáles antes de guardar (O1)', () => {
    const { api, dialogo, el } = crear('AdministradorGimnasio', true, CON_AFECTADOS, true);
    boton(el, 'Guardar')!.click();

    expect(dialogo.open).toHaveBeenCalledWith(ConfirmarDialog, expect.objectContaining({
      data: expect.objectContaining({ mensaje: expect.stringContaining('12 ejercicios propios quedan sin poder usarse') }),
    }));
    const mensaje = (dialogo.open.mock.calls[0] as unknown as [unknown, { data: { mensaje: string } }])[1].data.mensaje;
    expect(mensaje).toContain('Curl propio, Remo propio y 10 más');
    expect(mensaje).toContain('Mancuernas');
    expect(api.guardar).toHaveBeenCalled();
  });

  it('si el Dueño cancela el aviso, no se guarda', () => {
    const { api, el } = crear('AdministradorGimnasio', true, CON_AFECTADOS, false);
    boton(el, 'Guardar')!.click();
    expect(api.guardar).not.toHaveBeenCalled();
  });

  it('el Dueño renombra y da de baja solo sus tipos propios', () => {
    const { fixture, api, el } = crear('AdministradorGimnasio', true);
    expect(el.querySelector('li[data-clave="BARRA"] button.renombrar-tipo')).toBeNull();
    expect(el.querySelector('li[data-clave="GIM_KETTLEBELL"] button.baja-tipo')).not.toBeNull();

    (el.querySelector('li[data-clave="GIM_KETTLEBELL"] button.renombrar-tipo') as HTMLButtonElement).click();
    fixture.detectChanges();
    fixture.componentInstance.nombreEditado.setValue('Pesa rusa');
    (el.querySelector('button.ok-nombre') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(api.editarTipo).toHaveBeenCalledWith('t-k', 'Pesa rusa');
    expect(el.querySelector('li[data-clave="GIM_KETTLEBELL"]')!.textContent).toContain('Pesa rusa');

    (el.querySelector('li[data-clave="GIM_KETTLEBELL"] button.baja-tipo') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(api.darDeBajaTipo).toHaveBeenCalledWith('t-k');
    expect(el.querySelector('li[data-clave="GIM_KETTLEBELL"]')).toBeNull();
  });

  it('el Personal Trainer lo ve en solo lectura, sin Guardar, alta ni gestión de tipos', () => {
    const { el } = crear('Entrenador', false);

    expect(el.textContent).toContain('Solo lectura');
    expect(toggle(el, 'BARRA').disabled).toBe(true);
    expect(boton(el, 'Guardar')).toBeUndefined();
    expect(el.querySelector('form.nuevo')).toBeNull();
    expect(el.querySelector('button.renombrar-tipo, button.baja-tipo')).toBeNull();
  });

  it('el Administrador de la Plataforma abre la pantalla, elige el gimnasio y lo edita (D2)', () => {
    const { fixture, api, acceso, el } = crear('SuperAdministrador', true);

    expect(acceso.listarGimnasios).toHaveBeenCalled();
    expect(api.obtener).not.toHaveBeenCalled();
    expect(el.querySelector('mat-select')).not.toBeNull();
    expect(el.textContent).toContain('Elegí un gimnasio');

    fixture.componentInstance.gimnasio.setValue('g1');
    fixture.componentInstance.cargar('g1');
    fixture.detectChanges();

    expect(api.obtener).toHaveBeenCalledWith('g1');
    expect(el.textContent).not.toContain('Solo lectura');
    // También gestiona los tipos globales (no los siempre disponibles para la baja).
    expect(el.querySelector('li[data-clave="BARRA"] button.renombrar-tipo')).not.toBeNull();
    expect(el.querySelector('li[data-clave="PESO_CORPORAL"] button.baja-tipo')).toBeNull();

    fixture.componentInstance.nuevoTipo.setValue('Soga');
    fixture.componentInstance.agregarTipo();
    expect(api.crearTipo).toHaveBeenCalledWith('Soga', 'g1');

    boton(el, 'Guardar')!.click();
    expect(api.previsualizar).toHaveBeenCalledWith(expect.any(Array), 'g1');
    expect(api.guardar).toHaveBeenCalledWith(expect.any(Array), 'g1');
  });
});
