import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { InvitacionInfo } from '../../core/auth/auth.models';
import { AuthService } from '../../core/auth/auth.service';
import { ActivarInvitacion } from './activar-invitacion';

const INFO: InvitacionInfo = {
  nombre: 'Sofía', apellido: 'Invitada', email: 'sofia.invitada@test.local', gimnasio: 'GymBro Demo',
  expiraUtc: '2026-11-19T10:00:00Z',
};

function crear(token = 'tok-123') {
  const fixture = TestBed.createComponent(ActivarInvitacion);
  fixture.componentRef.setInput('token', token);
  fixture.detectChanges();
  return fixture;
}

describe('pantalla de activación de cuenta (etapa 6)', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([{ path: 'mi', children: [] }])],
    });
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('consulta la invitación por POST (el código no viaja en la URL) y muestra el gimnasio', () => {
    const fixture = crear();
    const req = http.expectOne('/api/auth/invitacion/consultar');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ token: 'tok-123' });
    req.flush(INFO);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('GymBro Demo');
    expect(fixture.nativeElement.textContent).toContain('Activá tu cuenta');
  });

  it('una invitación inválida muestra el motivo que informa la API y no ofrece el formulario', () => {
    const fixture = crear('malo');
    http.expectOne('/api/auth/invitacion/consultar').flush(
      { title: 'Solicitud inválida', detail: 'La invitación no es válida o ya venció.' },
      { status: 400, statusText: 'Bad Request' },
    );
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('La invitación no es válida o ya venció.');
    expect(fixture.nativeElement.querySelector('form')).toBeNull();
  });

  it('no envía nada si las contraseñas no coinciden o falta el consentimiento', () => {
    const fixture = crear();
    http.expectOne('/api/auth/invitacion/consultar').flush(INFO);
    fixture.detectChanges();

    const c = fixture.componentInstance;
    c.form.setValue({ password: 'Clave-1234', repetir: 'Clave-9999', fechaNacimiento: '1995-03-14', genero: 'F', consentimiento: true });
    c.activar();
    c.form.patchValue({ repetir: 'Clave-1234', consentimiento: false });
    c.activar();

    http.expectNone('/api/auth/invitacion/activar');
    expect(c.enviando()).toBe(false);
  });

  it('con datos válidos activa la cuenta, deja la sesión guardada y lleva al socio a su inicio', async () => {
    const fixture = crear();
    http.expectOne('/api/auth/invitacion/consultar').flush(INFO);
    fixture.detectChanges();

    const c = fixture.componentInstance;
    c.form.setValue({ password: 'Clave-1234', repetir: 'Clave-1234', fechaNacimiento: '1995-03-14', genero: 'F', consentimiento: true });
    c.activar();

    const req = http.expectOne('/api/auth/invitacion/activar');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      token: 'tok-123', password: 'Clave-1234', fechaNacimiento: '1995-03-14', genero: 'F', consentimientoDatosSalud: true,
    });
    req.flush({
      token: 'jwt', rol: 'Socio', nombreCompleto: 'Sofía Invitada', gimnasioId: null, socioId: 's1',
      expiraUtc: new Date(Date.now() + 60 * 60_000).toISOString(),
    });

    const auth = TestBed.inject(AuthService);
    expect(auth.rol()).toBe('Socio');
    expect(auth.token()).toBe('jwt');

    await fixture.whenStable();
    // Etapa 8: el Socio entra a su área /mi (PWA).
    expect(TestBed.inject(Router).url).toBe('/mi');
  });
});
