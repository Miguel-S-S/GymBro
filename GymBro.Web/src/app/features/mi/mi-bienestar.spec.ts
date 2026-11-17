import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { BienestarHoy, RegistroBienestar, RegistroBienestarRequest } from '../../core/models/bienestar.models';
import { guardarBorradorBienestar, leerBorradorBienestar } from '../../core/util/borrador-bienestar';
import { MiBienestar } from './mi-bienestar';

// Datos sintéticos de prueba.
const HOY = '2026-11-17';
const REGISTRO: RegistroBienestar = {
  id: 'r1', socioId: 's1', fecha: HOY, horasSueno: 6.5, estres: 4, fatiga: 5, pesoKg: 72.5,
  nutricionCategoria: 'Habitual', ingestaAguaLitros: 2, nutricionNotas: null, frecuenciaCardiacaReposo: 58,
  fechaAlta: '2026-11-17T12:00:00Z', fechaModificacion: null,
};

describe('MiBienestar (pantalla ¿Cómo estás hoy?)', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    localStorage.setItem('gymbro.sesion', JSON.stringify({
      token: 't', rol: 'Socio', nombreCompleto: 'Ana', gimnasioId: null, socioId: 's1',
      expiraUtc: new Date(Date.now() + 3_600_000).toISOString(),
    }));
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([{ path: 'mi', children: [] }, { path: 'login', children: [] }])],
    });
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => { http.verify(); localStorage.clear(); vi.restoreAllMocks(); });

  async function abrir(respuesta: BienestarHoy) {
    const fixture = TestBed.createComponent(MiBienestar);
    http.expectOne('/api/bienestar/hoy').flush(respuesta);
    fixture.detectChanges();
    await fixture.whenStable();
    return fixture;
  }

  it('sin registro del día: guarda lo elegido con PUT a la fecha local del servidor y borra el borrador', async () => {
    const fixture = await abrir({ fechaLocal: HOY, registro: null });
    const c = fixture.componentInstance;
    expect(c.registroId()).toBeNull();
    expect(c.motivo()).toBe('Cargá al menos un dato.'); // botón deshabilitado

    c.ajustarSueno(1);         // 7 h
    c.ajustarSueno(1);         // 7,5 h
    c.elegir('estres', 3);
    c.elegir('fatiga', 2);
    c.elegirIngesta('Alta');
    expect(leerBorradorBienestar('s1', HOY)).not.toBeNull(); // cada cambio queda en el dispositivo

    const navegar = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    c.guardar();
    const req = http.expectOne((r) => r.method === 'PUT' && r.url === `/api/bienestar/${HOY}`);
    expect(req.request.body as RegistroBienestarRequest).toEqual({
      horasSueno: 7.5, estres: 3, fatiga: 2, pesoKg: null, nutricionCategoria: 'Alta',
      ingestaAguaLitros: null, frecuenciaCardiacaReposo: null, nutricionNotas: null,
    });
    req.flush({ ...REGISTRO, horasSueno: 7.5, estres: 3, fatiga: 2 });
    await fixture.whenStable();

    expect(leerBorradorBienestar('s1')).toBeNull();
    expect(navegar).toHaveBeenCalledWith('/mi');
  });

  it('con registro del día: lo muestra para editarlo y guarda los cambios sobre la misma fecha', async () => {
    const fixture = await abrir({ fechaLocal: HOY, registro: REGISTRO });
    const c = fixture.componentInstance;
    expect(c.registroId()).toBe('r1');
    expect(c.form()).toMatchObject({ horasSueno: 6.5, estres: 4, fatiga: 5, pesoKg: 72.5, frecuenciaCardiacaReposo: 58 });
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('editando tu registro');

    c.elegir('estres', 4); // tocar el elegido lo quita
    c.guardar();
    const req = http.expectOne(`/api/bienestar/${HOY}`);
    expect(req.request.method).toBe('PUT');
    expect((req.request.body as RegistroBienestarRequest).estres).toBeNull();
    expect((req.request.body as RegistroBienestarRequest).fatiga).toBe(5);
    req.flush(REGISTRO);
  });

  it('recargar la pantalla recupera lo cargado y no enviado (borrador del mismo día)', async () => {
    const primera = await abrir({ fechaLocal: HOY, registro: REGISTRO });
    primera.componentInstance.elegir('fatiga', 7);
    primera.destroy();

    const segunda = await abrir({ fechaLocal: HOY, registro: REGISTRO });
    expect(segunda.componentInstance.form().fatiga).toBe(7);
    expect(segunda.componentInstance.aviso()).toContain('todavía no enviaste');
  });

  it('ignora el borrador de otro día', async () => {
    guardarBorradorBienestar('s1', { fecha: '2026-11-16', formulario: { estres: 7 } });
    const fixture = await abrir({ fechaLocal: HOY, registro: null });
    expect(fixture.componentInstance.form().estres).toBeNull();
  });

  it('sin conexión no envía y conserva el borrador', async () => {
    const fixture = await abrir({ fechaLocal: HOY, registro: null });
    const c = fixture.componentInstance;
    c.elegir('estres', 2);
    vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false);

    c.guardar();
    http.expectNone(`/api/bienestar/${HOY}`);
    expect(c.aviso()).toContain('Sin conexión');
    expect(leerBorradorBienestar('s1', HOY)?.formulario).toMatchObject({ estres: 2 });
  });

  it('si la red se cae al enviar, avisa y conserva el borrador', async () => {
    const fixture = await abrir({ fechaLocal: HOY, registro: null });
    const c = fixture.componentInstance;
    c.elegir('fatiga', 3);
    c.guardar();
    http.expectOne(`/api/bienestar/${HOY}`).error(new ProgressEvent('error'), { status: 0 });
    await fixture.whenStable();
    expect(c.aviso()).toContain('Sin conexión');
    expect(leerBorradorBienestar('s1', HOY)).not.toBeNull();
  });

  it('si no puede consultar el día, permite completar sobre el borrador', async () => {
    guardarBorradorBienestar('s1', { fecha: HOY, formulario: { horasSueno: 8 } });
    const fixture = TestBed.createComponent(MiBienestar);
    http.expectOne('/api/bienestar/hoy').error(new ProgressEvent('error'), { status: 0 });
    fixture.detectChanges();
    await fixture.whenStable();
    expect(fixture.componentInstance.fecha()).toBe(HOY);
    expect(fixture.componentInstance.form().horasSueno).toBe(8);
    expect(fixture.componentInstance.aviso()).toContain('Sin conexión');
  });
});
