import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { CanActivateFn, Route, Router, UrlTree, provideRouter } from '@angular/router';
import { routes } from './app.routes';
import { Rol, Sesion } from './core/auth/auth.models';

/**
 * Etapa 8c, T2: guardas de rol de la configuración real de rutas (app.routes.ts).
 * Se evalúan las guardas de la ruta y de sus ancestros (Shell: authGuard), sin cargar componentes.
 */

function guardar(rol: Rol): void {
  const s: Sesion = { token: 'tok', rol, nombreCompleto: 'T', gimnasioId: null, socioId: null,
    expiraUtc: new Date(Date.now() + 30 * 60_000).toISOString() };
  localStorage.setItem('gymbro.sesion', JSON.stringify(s));
}

/** Guardas que se ejecutan para llegar a `ruta` (padres primero), buscando por el path completo. */
function guardasDe(ruta: string, lista: Route[] = routes, prefijo = '', acumuladas: CanActivateFn[] = []): CanActivateFn[] | null {
  for (const r of lista) {
    const path = [prefijo, r.path ?? ''].filter((p) => p !== '').join('/');
    const guardas = [...acumuladas, ...((r.canActivate ?? []) as CanActivateFn[])];
    if (path === ruta && !r.children) return guardas;
    if (r.children && (ruta === path || ruta.startsWith(path === '' ? '' : `${path}/`) || path === '')) {
      const encontrada = guardasDe(ruta, r.children, path, guardas);
      if (encontrada) return encontrada;
    }
  }
  return null;
}

/** true si todas las guardas permiten el paso; si alguna redirige, devuelve la URL de destino. */
function navegar(ruta: string): true | string {
  const guardas = guardasDe(ruta);
  if (!guardas) throw new Error(`La ruta ${ruta} no existe en app.routes.ts`);
  for (const g of guardas) {
    const r = TestBed.runInInjectionContext(() => g({} as never, {} as never));
    if (r instanceof UrlTree) return TestBed.inject(Router).serializeUrl(r);
    if (r !== true) throw new Error('Guarda con resultado inesperado');
  }
  return true;
}

const PANTALLAS_DEL_PERSONAL = ['socios', 'socios/:id', 'socios/:id/sesiones/nueva', 'sesiones/:id', 'ejercicios'];

describe('rutas por rol (Etapa 8c, T2)', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
  });

  it.each(PANTALLAS_DEL_PERSONAL)('un Socio que abre /%s es redirigido a /mi', (ruta) => {
    guardar('Socio');
    expect(navegar(ruta)).toBe('/mi');
  });

  it.each(PANTALLAS_DEL_PERSONAL)('el Dueño de Gimnasio conserva /%s', (ruta) => {
    guardar('AdministradorGimnasio');
    expect(navegar(ruta)).toBe(true);
  });

  it.each(PANTALLAS_DEL_PERSONAL)('el Personal Trainer conserva /%s', (ruta) => {
    guardar('Entrenador');
    expect(navegar(ruta)).toBe(true);
  });

  // Acceso actual del Administrador de la Plataforma, sin cambios: sus pantallas propias sí, las de socios no
  // (datos de salud, Ley 25.326).
  it.each(['plataforma/gimnasios', 'plataforma/auditoria'])('el Administrador de la Plataforma conserva /%s', (ruta) => {
    guardar('SuperAdministrador');
    expect(navegar(ruta)).toBe(true);
  });

  it.each(PANTALLAS_DEL_PERSONAL)('el Administrador de la Plataforma sigue sin acceso a /%s (sin cambios)', (ruta) => {
    guardar('SuperAdministrador');
    expect(navegar(ruta)).toBe('/plataforma/gimnasios');
  });

  it('el Socio conserva su área /mi', () => {
    guardar('Socio');
    expect(navegar('mi/entrenar')).toBe(true);
  });

  it('el Personal Trainer no entra al área /mi', () => {
    guardar('Entrenador');
    expect(navegar('mi/entrenar')).toBe('/socios');
  });

  it('sin sesión, las pantallas del personal llevan a /login', () => {
    expect(navegar('socios')).toBe('/login');
  });
});
