import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { EjercicioResumen } from '../../core/models/ejercicio.models';
import { SesionRegistro } from './sesion-registro';

// Etapa 9a-1. Datos sintéticos de prueba.
const PLANCHA: EjercicioResumen = { id: 'ej-p', nombre: 'Plancha', zonaObjetivo: 'Core', musculoPrincipal: 'Recto abdominal',
  equipamiento: 'Body Weight', dificultad: 2, tipoMedida: 'Segundos', origen: 'Proyecto' };
const CRUNCH: EjercicioResumen = { ...PLANCHA, id: 'ej-c', nombre: 'Crunch abdominal', dificultad: 1, tipoMedida: 'Repeticiones' };

describe('SesionRegistro: tipo de medida', () => {
  beforeEach(() => {
    localStorage.setItem('gymbro.sesion', JSON.stringify({
      token: 't', rol: 'AdministradorGimnasio', nombreCompleto: 'Dueño', gimnasioId: 'g1', socioId: null,
      expiraUtc: new Date(Date.now() + 3_600_000).toISOString(),
    }));
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
  });
  afterEach(() => localStorage.clear());

  async function conEjercicio(e: EjercicioResumen) {
    const fixture = TestBed.createComponent(SesionRegistro);
    fixture.componentRef.setInput('id', 's1');
    fixture.componentInstance.ejercicios.at(0).controls.ejercicio.setValue(e);
    fixture.detectChanges();
    await fixture.whenStable();
    const seccion = (fixture.nativeElement as HTMLElement).querySelector('section.ejercicio')!;
    return {
      encabezado: seccion.querySelector('.encabezado-series')!.textContent ?? '',
      campo: seccion.querySelector('input[formControlName="repeticiones"]')!.getAttribute('aria-label'),
    };
  }

  it('con un ejercicio por segundos rotula la columna "Seg." y el campo "Segundos"', async () => {
    const { encabezado, campo } = await conEjercicio(PLANCHA);
    expect(encabezado).toContain('Seg.');
    expect(campo).toBe('Segundos');
  });

  it('con un ejercicio por repeticiones rotula "Reps" y "Repeticiones"', async () => {
    const { encabezado, campo } = await conEjercicio(CRUNCH);
    expect(encabezado).toContain('Reps');
    expect(campo).toBe('Repeticiones');
  });
});
