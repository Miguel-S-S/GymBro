import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { ROL_ETIQUETA, Sesion, rutaInicial } from './auth.models';
import { AuthService } from './auth.service';
import { mensajeDeError } from '../util/api-error';

function sesion(minutos: number, rol: Sesion['rol'] = 'Entrenador'): Sesion {
  return {
    token: 't', rol, nombreCompleto: 'Test', gimnasioId: null, socioId: null,
    expiraUtc: new Date(Date.now() + minutos * 60_000).toISOString(),
  };
}

describe('roles', () => {
  it('cada rol tiene etiqueta de interfaz y pantalla inicial', () => {
    for (const rol of Object.keys(ROL_ETIQUETA) as Sesion['rol'][]) {
      expect(ROL_ETIQUETA[rol]).toBeTruthy();
      expect(rutaInicial(rol).startsWith('/')).toBe(true);
    }
  });

  it('el administrador de la plataforma no aterriza en pantallas de socios', () => {
    expect(rutaInicial('SuperAdministrador')).toBe('/plataforma/gimnasios');
  });
});

describe('AuthService', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideRouter([])] });
  });

  it('descarta una sesión vencida guardada en el navegador', () => {
    localStorage.setItem('gymbro.sesion', JSON.stringify(sesion(-5)));
    const auth = TestBed.inject(AuthService);
    expect(auth.autenticado()).toBe(false);
    expect(auth.rol()).toBeNull();
  });

  it('restaura una sesión vigente y expone su rol', () => {
    localStorage.setItem('gymbro.sesion', JSON.stringify(sesion(30, 'AdministradorGimnasio')));
    const auth = TestBed.inject(AuthService);
    expect(auth.autenticado()).toBe(true);
    expect(auth.tieneRol('AdministradorGimnasio')).toBe(true);
    expect(auth.tieneRol('Socio', 'Entrenador')).toBe(false);
  });
});

describe('mensajeDeError (ProblemDetails de la API)', () => {
  it('prefiere detail sobre title', () => {
    const e = new HttpErrorResponse({ status: 400, error: { title: 'Solicitud inválida', detail: 'Ya existe una cuenta con ese email.' } });
    expect(mensajeDeError(e)).toBe('Ya existe una cuenta con ese email.');
  });

  it('traduce 403', () => {
    expect(mensajeDeError(new HttpErrorResponse({ status: 403 }))).toContain('permisos');
  });
});