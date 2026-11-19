import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { MiHoy } from './mi-hoy';

// Datos sintéticos de prueba.
describe('MiHoy: tarjeta ¿Cómo estás hoy? (Etapa 8b)', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    localStorage.setItem('gymbro.sesion', JSON.stringify({
      token: 't', rol: 'Socio', nombreCompleto: 'Ana', gimnasioId: null, socioId: 's1',
      expiraUtc: new Date(Date.now() + 3_600_000).toISOString(),
    }));
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => { http.verify(); localStorage.clear(); });

  async function abrir(registro: object | null) {
    const fixture = TestBed.createComponent(MiHoy);
    http.expectOne('/api/bienestar/hoy').flush({ fechaLocal: '2026-11-17', registro });
    http.expectOne('/api/Socio/s1/prescripciones').flush([]);
    fixture.detectChanges();
    // Etapa 10a: la tarjeta de avisos consulta las alertas pendientes del socio.
    http.expectOne('/api/alertas?estado=Pendiente').flush([]);
    await fixture.whenStable();
    const tarjeta = (fixture.nativeElement as HTMLElement).querySelector('.bienestar')!;
    return { texto: tarjeta.textContent ?? '', enlace: tarjeta.querySelector('a')!.getAttribute('href') };
  }

  it('sin registro de hoy ofrece cargarlo en /mi/bienestar', async () => {
    const { texto, enlace } = await abrir(null);
    expect(texto).toContain('¿Cómo estás hoy?');
    expect(texto).toContain('Cargar');
    expect(enlace).toBe('/mi/bienestar');
  });

  it('con registro de hoy ofrece editarlo', async () => {
    const { texto } = await abrir({ id: 'r1' });
    expect(texto).toContain('Ya cargaste tu registro de hoy');
    expect(texto).toContain('Editar');
  });
});
