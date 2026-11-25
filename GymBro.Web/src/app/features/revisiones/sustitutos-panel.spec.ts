import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { SugerenciaSustitutos, SustitutosApi, SustitutosPanel } from './sustitutos-panel';

// Etapa 9b-2. Datos sinteticos de prueba.
function respuesta(p: Partial<SugerenciaSustitutos>): SugerenciaSustitutos {
  return {
    lineaId: 'l1', ejercicioActual: 'Sentadilla', candidatos: [{ id: 'c1', nombre: 'Prensa' }],
    sugerencias: [{ ejercicioId: 'c1', nombre: 'Prensa', zonaObjetivo: 'Legs', equipamiento: 'Maquina', justificacion: 'Sin carga axial.' }],
    rechazadasPorLimites: [], respetaConjunto: true, descartada: false, motivoDescarte: null, modelo: 'm', latenciaMs: 10, ...p,
  };
}

async function montar(api: { sugerir: ReturnType<typeof vi.fn> }) {
  TestBed.configureTestingModule({ providers: [{ provide: SustitutosApi, useValue: api }] });
  const f = TestBed.createComponent(SustitutosPanel);
  f.componentRef.setInput('rutinaId', 'r1');
  f.componentRef.setInput('lineaId', 'l1');
  f.detectChanges();
  const texto = () => ((f.nativeElement as HTMLElement).textContent ?? '').replace(/\s+/g, ' ');
  return { f, texto };
}

describe('SustitutosPanel (Etapa 9b-2)', () => {
  it('muestra la sugerencia con su justificacion y la aclaracion de que no se aplica sola', async () => {
    const api = { sugerir: vi.fn(() => of(respuesta({}))) };
    const { f, texto } = await montar(api);
    f.componentInstance.abierto.set(true);
    f.componentInstance.motivo = 'molestia lumbar';
    f.componentInstance.pedir();
    f.detectChanges();
    expect(api.sugerir).toHaveBeenCalledWith('r1', 'l1', 'molestia lumbar');
    expect(texto()).toContain('Prensa');
    expect(texto()).toContain('Sin carga axial.');
    expect(texto()).toContain('No se aplica sola');
  });

  it('una salida descartada se informa sin mostrar sugerencias', async () => {
    const api = { sugerir: vi.fn(() => of(respuesta({ descartada: true, motivoDescarte: 'candidato_fuera_del_conjunto', sugerencias: [] }))) };
    const { f, texto } = await montar(api);
    f.componentInstance.abierto.set(true);
    f.componentInstance.motivo = 'molestia';
    f.componentInstance.pedir();
    f.detectChanges();
    expect(texto()).toContain('fuera de los candidatos recuperados');
  });

  it('con el modelo caido muestra el mensaje de la API', async () => {
    const error = new HttpErrorResponse({ status: 503, error: { detail: 'El modelo de lenguaje local no está disponible en este momento.' } });
    const api = { sugerir: vi.fn(() => throwError(() => error)) };
    const { f, texto } = await montar(api);
    f.componentInstance.abierto.set(true);
    f.componentInstance.motivo = 'molestia';
    f.componentInstance.pedir();
    f.detectChanges();
    expect(texto()).toContain('no está disponible');
  });
});
