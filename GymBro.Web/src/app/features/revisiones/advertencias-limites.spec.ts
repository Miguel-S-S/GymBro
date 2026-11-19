import { TestBed } from '@angular/core/testing';
import { ViolacionLimite } from '../../core/models/alertas.models';
import { AdvertenciasLimites } from './advertencias-limites';

// Etapa 10a. Datos sintéticos de prueba.
function crear(violaciones: ViolacionLimite[]) {
  const fixture = TestBed.createComponent(AdvertenciasLimites);
  fixture.componentRef.setInput('violaciones', violaciones);
  fixture.detectChanges();
  return fixture.nativeElement as HTMLElement;
}

describe('Panel "Advertencias de límites" de la revisión (Etapa 10a)', () => {
  it('no aparece si la rutina cumple los límites', () => {
    expect(crear([]).querySelector('.advertencias-limites')).toBeNull();
  });

  it('aparece con cada violación y aclara que no bloquea', () => {
    const el = crear([
      { codigo: 'LIM-SERIES', mensaje: 'Press: 8 series; el rango admitido es 1 a 6.', orden: 1 },
      { codigo: 'LIM-RIR-PRINCIPIANTE', mensaje: 'Press: RIR objetivo 1; para un socio principiante el mínimo es 2.', orden: 1 },
    ]);
    const panel = el.querySelector('.advertencias-limites')!;
    expect(panel.textContent).toContain('Advertencias de límites');
    expect(panel.querySelectorAll('li')).toHaveLength(2);
    expect(panel.textContent).toContain('Podés aprobar igual');
  });
});
