import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { SociosApi } from '../../core/api/socios.api';
import { Rol, Sesion } from '../../core/auth/auth.models';
import { SocioResumen } from '../../core/models/socio.models';
import { SociosLista } from './socios-lista';

// Etapa 9a-2b: estado administrativo del socio (informativo). Datos sintéticos de prueba.
const SOCIOS: SocioResumen[] = [
  { id: 's-a', nombreCompleto: 'Ana Prueba', email: 'ana@prueba.test', fechaAlta: '2026-11-01T00:00:00Z', estadoAcceso: 'Activo', estado: 'Activo' },
  { id: 's-b', nombreCompleto: 'Bruno Prueba', email: 'bruno@prueba.test', fechaAlta: '2026-11-02T00:00:00Z', estadoAcceso: 'Invitado', estado: 'Inactivo' },
];

function guardarSesion(rol: Rol): void {
  const s: Sesion = { token: 'tok', rol, nombreCompleto: 'T', gimnasioId: 'g1', socioId: null,
    expiraUtc: new Date(Date.now() + 30 * 60_000).toISOString() };
  localStorage.setItem('gymbro.sesion', JSON.stringify(s));
}

function crear(rol: Rol) {
  localStorage.clear();
  guardarSesion(rol);
  const api = { listar: vi.fn(() => of(SOCIOS)), cambiarEstado: vi.fn(() => of({})) };
  TestBed.configureTestingModule({
    providers: [provideRouter([]), { provide: SociosApi, useValue: api }, { provide: MatDialog, useValue: { open: vi.fn() } }],
  });
  const fixture = TestBed.createComponent(SociosLista);
  fixture.detectChanges();
  return { fixture, api, el: fixture.nativeElement as HTMLElement };
}

const filas = (el: HTMLElement) => Array.from(el.querySelectorAll('tr.mat-mdc-row'));

describe('Lista de socios: estado administrativo (Etapa 9a-2b)', () => {
  it('muestra el estado de cada socio', () => {
    const { el } = crear('Entrenador');
    expect(filas(el).map((f) => f.querySelector('.estado-socio .insignia')?.textContent?.trim())).toEqual(['Activo', 'Inactivo']);
  });

  it('el Dueño lo cambia desde la lista sin abrir la ficha', () => {
    const { fixture, api, el } = crear('AdministradorGimnasio');
    (filas(el)[0].querySelector('button.cambiar-estado') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(api.cambiarEstado).toHaveBeenCalledWith('s-a', 'Inactivo');
    expect(filas(el)[0].querySelector('.estado-socio .insignia')!.textContent!.trim()).toBe('Inactivo');
  });

  it('el Personal Trainer lo ve, pero no lo cambia', () => {
    const { el } = crear('Entrenador');
    expect(el.querySelector('button.cambiar-estado')).toBeNull();
  });
});
