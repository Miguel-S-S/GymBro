import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { of } from 'rxjs';
import { AccesoApi } from '../../core/api/acceso.api';
import { SociosApi } from '../../core/api/socios.api';
import { Rol, Sesion } from '../../core/auth/auth.models';
import { Gimnasio } from '../../core/models/acceso.models';
import { MiGimnasio } from './mi-gimnasio';

// Etapa 9a-2c. Datos sintéticos de prueba.
const G2: Gimnasio = { id: 'g2', nombre: 'Gimnasio Dos', codigo: 'BBB222', requiereAprobacionRutinas: false, activo: true, fechaAlta: '' };

function guardarSesion(rol: Rol): void {
  const s: Sesion = { token: 'tok', rol, nombreCompleto: 'T', gimnasioId: null, socioId: null,
    expiraUtc: new Date(Date.now() + 30 * 60_000).toISOString() };
  localStorage.setItem('gymbro.sesion', JSON.stringify(s));
}

function crear(rol: Rol) {
  guardarSesion(rol);
  const api = {
    listarGimnasios: vi.fn(() => of([G2])),
    miGimnasio: vi.fn(() => of(G2)),
    actualizarPoliticaRevision: vi.fn(() => of({ ...G2, requiereAprobacionRutinas: true })),
    listarEntrenadores: vi.fn(() => of([{ id: 'e1', nombreCompleto: 'Entrenador Prueba', email: 'e@prueba.test' }])),
    crearEntrenador: vi.fn(),
    asignarSocio: vi.fn(),
  };
  const socios = { listar: vi.fn(() => of([])) };
  TestBed.configureTestingModule({
    providers: [
      { provide: AccesoApi, useValue: api },
      { provide: SociosApi, useValue: socios },
      { provide: MatSnackBar, useValue: { open: vi.fn() } },
    ],
  });
  const fixture = TestBed.createComponent(MiGimnasio);
  fixture.detectChanges();
  return { fixture, api, socios, el: fixture.nativeElement as HTMLElement };
}

describe('Mi gimnasio (Etapa 9a-2c)', () => {
  beforeEach(() => localStorage.clear());

  it('para el Dueño no hay selector de gimnasio y mantiene entrenadores y asignaciones', () => {
    const { api, socios, el } = crear('AdministradorGimnasio');
    expect(el.querySelector('.selector')).toBeNull();
    expect(api.listarGimnasios).not.toHaveBeenCalled();
    expect(api.miGimnasio).toHaveBeenCalledWith(null);
    expect(socios.listar).toHaveBeenCalled();
    expect(el.textContent).toContain('Entrenador Prueba');
    expect(el.querySelector('.aviso-script')).toBeNull();
    expect(el.querySelectorAll('form')).toHaveLength(2);
  });

  it('el Dueño cambia la política sin indicar gimnasio', () => {
    const { fixture, api } = crear('AdministradorGimnasio');
    fixture.componentInstance.cambiarPolitica(true);
    expect(api.actualizarPoliticaRevision).toHaveBeenCalledWith(true, null);
  });

  it('el Administrador de la Plataforma ve el selector y, sin gimnasio elegido, "Elegí un gimnasio"', () => {
    const { api, el } = crear('SuperAdministrador');
    expect(el.querySelector('.selector')).not.toBeNull();
    expect(api.miGimnasio).not.toHaveBeenCalled();
    expect(el.querySelector('.sin-gimnasio')!.textContent).toContain('Elegí un gimnasio');
  });

  it('el Administrador de la Plataforma elige un gimnasio, cambia la política indicándolo y ve el aviso de scripts', () => {
    const { fixture, api, socios, el } = crear('SuperAdministrador');
    fixture.componentInstance.gimnasioElegido.setValue('g2');
    fixture.componentInstance.cargar();
    fixture.detectChanges();

    expect(api.miGimnasio).toHaveBeenCalledWith('g2');
    expect(el.querySelector('h1')!.textContent).toContain('Gimnasio Dos');
    expect(el.textContent).toContain('BBB222');
    const toggle = el.querySelector('mat-slide-toggle button[role="switch"]') as HTMLButtonElement;
    expect(toggle.disabled).toBe(false);
    toggle.click();
    fixture.detectChanges();
    expect(api.actualizarPoliticaRevision).toHaveBeenCalledWith(true, 'g2');

    // Decisión P2: las secciones se muestran con el aviso de script (no se ocultan) y no se llaman endpoints del Dueño.
    expect(el.querySelector('.entrenadores .aviso-script')!.textContent).toContain('admin-usuario.sql');
    expect(el.querySelector('.asignacion .aviso-script')!.textContent).toContain('admin-asignacion.sql');
    expect(api.listarEntrenadores).not.toHaveBeenCalled();
    expect(socios.listar).not.toHaveBeenCalled();
  });
});
