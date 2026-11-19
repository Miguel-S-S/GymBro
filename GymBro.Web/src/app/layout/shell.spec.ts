import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { Rol, Sesion } from '../core/auth/auth.models';
import { Shell } from './shell';

/** Etapa 8c, T8: ítems del menú lateral por rol. */

function guardar(rol: Rol): void {
  const s: Sesion = { token: 'tok', rol, nombreCompleto: 'T', gimnasioId: null, socioId: null,
    expiraUtc: new Date(Date.now() + 30 * 60_000).toISOString() };
  localStorage.setItem('gymbro.sesion', JSON.stringify(s));
}

function enlacesDelMenu(rol: Rol): string[] {
  guardar(rol);
  TestBed.configureTestingModule({
    imports: [Shell],
    providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
  });
  const fixture = TestBed.createComponent(Shell);
  fixture.detectChanges();
  const enlaces = (fixture.nativeElement as HTMLElement).querySelectorAll('mat-nav-list a');
  return Array.from(enlaces).map((a) => a.getAttribute('href') ?? '');
}

describe('menú lateral por rol (Etapa 8c, T8)', () => {
  beforeEach(() => localStorage.clear());

  it('el Administrador de la Plataforma ve Ejercicios, pero no Socios ni Rutinas a revisar', () => {
    const enlaces = enlacesDelMenu('SuperAdministrador');
    expect(enlaces).toContain('/ejercicios');
    expect(enlaces).toContain('/plataforma/gimnasios');
    expect(enlaces).not.toContain('/socios');
    expect(enlaces).not.toContain('/revisiones');
  });

  it('Etapa 9a-2: el Administrador de la Plataforma, el Dueño y el Personal Trainer ven Equipamiento', () => {
    expect(enlacesDelMenu('SuperAdministrador')).toContain('/gimnasio/equipamiento');
    TestBed.resetTestingModule();
    expect(enlacesDelMenu('AdministradorGimnasio')).toContain('/gimnasio/equipamiento');
    TestBed.resetTestingModule();
    expect(enlacesDelMenu('Entrenador')).toContain('/gimnasio/equipamiento');
  });

  it('el Personal Trainer conserva Socios y Ejercicios', () => {
    const enlaces = enlacesDelMenu('Entrenador');
    expect(enlaces).toEqual(expect.arrayContaining(['/socios', '/ejercicios', '/revisiones']));
    expect(enlaces).not.toContain('/plataforma/gimnasios');
  });
});
