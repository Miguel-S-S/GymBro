import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { SocioAlta, enlaceInvitacion } from '../models/socio.models';
import { SociosApi } from './socios.api';

describe('SociosApi: alta con invitación, edición y baja (etapa 6)', () => {
  let api: SociosApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(SociosApi);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('crear envía el DNI y devuelve la ficha junto con la invitación', () => {
    const alta: SocioAlta = {
      socio: { id: 's1', nombreCompleto: 'Sofía Invitada', email: 's@t.local', fechaAlta: '2026-11-12', estadoAcceso: 'Invitado' },
      invitacion: { token: 'abc-_123', expiraUtc: '2026-11-19T10:00:00Z' },
    };
    let recibido: SocioAlta | undefined;
    api.crear({ nombre: 'Sofía', apellido: 'Invitada', email: 's@t.local', dni: '35222111' }).subscribe((r) => (recibido = r));

    const req = http.expectOne('/api/Socio');
    expect(req.request.method).toBe('POST');
    expect(req.request.body.dni).toBe('35222111');
    req.flush(alta);
    expect(recibido?.invitacion.token).toBe('abc-_123');
    expect(recibido?.socio.estadoAcceso).toBe('Invitado');
  });

  it('actualizar usa PUT sobre el socio', () => {
    api.actualizar('s1', { nombre: 'A', apellido: 'B', email: 'a@b.com', dni: '30111222' }).subscribe();
    const req = http.expectOne('/api/Socio/s1');
    expect(req.request.method).toBe('PUT');
    req.flush({});
  });

  it('renovarInvitacion usa POST y baja usa DELETE', () => {
    api.renovarInvitacion('s1').subscribe();
    const r1 = http.expectOne('/api/Socio/s1/invitacion');
    expect(r1.request.method).toBe('POST');
    r1.flush({ token: 'x', expiraUtc: '2026-11-19T10:00:00Z' });

    api.baja('s1').subscribe();
    const r2 = http.expectOne('/api/Socio/s1');
    expect(r2.request.method).toBe('DELETE');
    r2.flush(null, { status: 204, statusText: 'No Content' });
  });

  it('enlaceInvitacion arma la ruta pública /activar/:token sin barras duplicadas', () => {
    expect(enlaceInvitacion('abc-_123', 'http://localhost:4200')).toBe('http://localhost:4200/activar/abc-_123');
    expect(enlaceInvitacion('abc', 'http://localhost:4200/')).toBe('http://localhost:4200/activar/abc');
    expect(enlaceInvitacion('a/b+c', 'https://gymbro.app')).toBe('https://gymbro.app/activar/a%2Fb%2Bc');
  });
});
