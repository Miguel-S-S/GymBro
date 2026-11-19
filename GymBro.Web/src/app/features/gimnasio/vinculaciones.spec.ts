import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { of } from 'rxjs';
import { AccesoApi } from '../../core/api/acceso.api';
import { Rol, Sesion } from '../../core/auth/auth.models';
import { SolicitudVinculacion } from '../../core/models/acceso.models';
import { Vinculaciones } from './vinculaciones';

// Etapa 9a-2c. Datos sintéticos de prueba.
const SOLICITUD: SolicitudVinculacion = {
  id: 'sv1', socioId: 's1', nombreSocio: 'Socio Prueba', dni: '40000001', gimnasioId: 'g2', estado: 'Pendiente',
  fechaAlta: '2026-11-19T10:00:00Z', motivo: null,
};

function guardarSesion(rol: Rol): void {
  const s: Sesion = { token: 'tok', rol, nombreCompleto: 'T', gimnasioId: null, socioId: null,
    expiraUtc: new Date(Date.now() + 30 * 60_000).toISOString() };
  localStorage.setItem('gymbro.sesion', JSON.stringify(s));
}

function crear(rol: Rol) {
  guardarSesion(rol);
  const api = {
    listarGimnasios: vi.fn(() => of([
      { id: 'g1', nombre: 'Gimnasio Uno', codigo: 'A', requiereAprobacionRutinas: true, activo: true, fechaAlta: '' },
      { id: 'g2', nombre: 'Gimnasio Dos', codigo: 'B', requiereAprobacionRutinas: false, activo: true, fechaAlta: '' },
    ])),
    solicitudesPendientes: vi.fn(() => of([SOLICITUD])),
    resolverSolicitud: vi.fn(() => of({ ...SOLICITUD, estado: 'Aprobada' })),
  };
  TestBed.configureTestingModule({
    providers: [{ provide: AccesoApi, useValue: api }, { provide: MatSnackBar, useValue: { open: vi.fn() } }],
  });
  const fixture = TestBed.createComponent(Vinculaciones);
  fixture.detectChanges();
  return { fixture, api, el: fixture.nativeElement as HTMLElement };
}

describe('Vinculaciones (Etapa 9a-2c)', () => {
  beforeEach(() => localStorage.clear());

  it('para el Dueño no hay selector y carga las solicitudes de su gimnasio', () => {
    const { api, el } = crear('AdministradorGimnasio');
    expect(el.querySelector('mat-select')).toBeNull();
    expect(api.listarGimnasios).not.toHaveBeenCalled();
    expect(api.solicitudesPendientes).toHaveBeenCalledWith(null);
    expect(el.textContent).toContain('Socio Prueba');
  });

  it('el Dueño resuelve sin indicar gimnasio', () => {
    const { fixture, api, el } = crear('AdministradorGimnasio');
    (el.querySelector('button.rechazar') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(api.resolverSolicitud).toHaveBeenCalledWith('sv1', false, 'Rechazada por el gimnasio', null);
  });

  it('el Administrador de la Plataforma ve el selector y, sin gimnasio elegido, "Elegí un gimnasio"', () => {
    const { api, el } = crear('SuperAdministrador');
    expect(el.querySelector('mat-select')).not.toBeNull();
    expect(api.listarGimnasios).toHaveBeenCalled();
    expect(api.solicitudesPendientes).not.toHaveBeenCalled();
    expect(el.querySelector('.sin-gimnasio')!.textContent).toContain('Elegí un gimnasio');
  });

  it('el Administrador de la Plataforma elige un gimnasio, ve sus solicitudes y aprueba indicando el gimnasio', () => {
    const { fixture, api, el } = crear('SuperAdministrador');
    fixture.componentInstance.gimnasio.setValue('g2');
    fixture.componentInstance.cargar();
    fixture.detectChanges();
    expect(api.solicitudesPendientes).toHaveBeenCalledWith('g2');
    expect(el.querySelector('.sin-gimnasio')).toBeNull();

    // No se oculta ni deshabilita nada: aprobar y rechazar están disponibles.
    const aprobar = el.querySelector('button.aprobar') as HTMLButtonElement;
    expect(aprobar.disabled).toBe(false);
    expect((el.querySelector('button.rechazar') as HTMLButtonElement).disabled).toBe(false);
    aprobar.click();
    fixture.detectChanges();
    expect(api.resolverSolicitud).toHaveBeenCalledWith('sv1', true, undefined, 'g2');
    expect(el.textContent).not.toContain('Socio Prueba');
  });
});
