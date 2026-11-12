import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, UrlTree, provideRouter } from '@angular/router';
import { authGuard, rolGuard } from './auth.guard';
import { authInterceptor } from './auth.interceptor';
import { Rol, Sesion } from './auth.models';
import { AuthService } from './auth.service';
import { paramsAuditoria } from '../models/acceso.models';

function guardar(rol: Rol, minutos = 30): void {
  const s: Sesion = { token: 'tok', rol, nombreCompleto: 'T', gimnasioId: null, socioId: null,
    expiraUtc: new Date(Date.now() + minutos * 60_000).toISOString() };
  localStorage.setItem('gymbro.sesion', JSON.stringify(s));
}

function ejecutar(guard: ReturnType<typeof rolGuard> | typeof authGuard): boolean | UrlTree {
  return TestBed.runInInjectionContext(() => guard({} as never, {} as never) as boolean | UrlTree);
}

describe('guardas de ruta (criterio de validación 4.6, etapa 5)', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
  });

  it('sin sesión se redirige a /login', () => {
    const r = ejecutar(authGuard);
    expect(r instanceof UrlTree && TestBed.inject(Router).serializeUrl(r)).toBe('/login');
  });

  it.each<Rol>(['Entrenador', 'Socio', 'AdministradorGimnasio'])('%s no accede a pantallas del Administrador de la Plataforma', (rol) => {
    guardar(rol);
    const r = ejecutar(rolGuard('SuperAdministrador'));
    expect(r).toBeInstanceOf(UrlTree);
    expect(TestBed.inject(Router).serializeUrl(r as UrlTree)).not.toContain('plataforma');
  });

  it('el Administrador de la Plataforma sí accede', () => {
    guardar('SuperAdministrador');
    expect(ejecutar(rolGuard('SuperAdministrador'))).toBe(true);
  });

  it('el Administrador de la Plataforma no accede a las pantallas de socios', () => {
    guardar('SuperAdministrador');
    expect(ejecutar(rolGuard('AdministradorGimnasio', 'Entrenador', 'Socio'))).toBeInstanceOf(UrlTree);
  });
});

describe('authInterceptor', () => {
  let http: HttpClient;
  let ctrl: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting(), provideRouter([{ path: 'login', children: [] }])],
    });
    http = TestBed.inject(HttpClient);
    ctrl = TestBed.inject(HttpTestingController);
  });

  it('envía Authorization: Bearer a la API', () => {
    guardar('Entrenador');
    TestBed.inject(AuthService);
    http.get('/api/socios').subscribe();
    expect(ctrl.expectOne('/api/socios').request.headers.get('Authorization')).toBe('Bearer tok');
  });

  it('no envía el token a otros dominios', () => {
    guardar('Entrenador');
    http.get('https://otro.example/x').subscribe();
    expect(ctrl.expectOne('https://otro.example/x').request.headers.has('Authorization')).toBe(false);
  });

  it('un 401 cierra la sesión', () => {
    guardar('Entrenador');
    const auth = TestBed.inject(AuthService);
    http.get('/api/socios').subscribe({ error: () => {} });
    ctrl.expectOne('/api/socios').flush(null, { status: 401, statusText: 'Unauthorized' });
    expect(auth.autenticado()).toBe(false);
  });

  it('un 401 en el login (credenciales inválidas) no cierra una sesión ajena', () => {
    guardar('Entrenador');
    const auth = TestBed.inject(AuthService);
    http.post('/api/auth/login', {}).subscribe({ error: () => {} });
    ctrl.expectOne('/api/auth/login').flush(null, { status: 401, statusText: 'Unauthorized' });
    expect(auth.autenticado()).toBe(true);
  });
});

describe('paramsAuditoria', () => {
  it('omite filtros vacíos y usa 200 por defecto', () => {
    expect(paramsAuditoria({ accion: ' ', entidad: '' })).toEqual({ cantidad: 200 });
  });

  it('"hasta" es inclusivo: se envía el día siguiente', () => {
    expect(paramsAuditoria({ hasta: '2026-11-30' })['hasta']).toBe('2026-12-01');
  });
});