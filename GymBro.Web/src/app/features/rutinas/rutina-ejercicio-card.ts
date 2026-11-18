import { Component, input } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RutinaEjercicio } from '../../core/models/acceso.models';
import { aplicarPaso, CampoRutina, enSegundos, resumenEjercicio } from '../../core/util/rutina-formato';

interface Control {
  campo: CampoRutina;
  etiqueta: string;
  paso: number;
  sufijo?: string;
}

/**
 * Una tarjeta por ejercicio con su prescripción en una línea (`3 series × 8–12 reps · RIR 2 · 90 s`).
 * En modo editable muestra botones +/− en lugar de una fila por serie; modifica el ejercicio recibido
 * (el componente padre decide cuándo enviarlo a la API).
 */
@Component({
  selector: 'app-rutina-ejercicio-card',
  imports: [MatButtonModule, MatIconModule],
  template: `
    <div class="tarjeta-ejercicio">
      <div class="titulo">
        <span class="orden">{{ ejercicio().orden }}</span>
        <strong>{{ ejercicio().ejercicio }}</strong>
      </div>

      @if (!editable()) {
        <div class="resumen">{{ resumen() }}</div>
      } @else {
        <div class="controles">
          @for (c of controles(); track c.campo) {
            <div class="control">
              <span class="etiqueta">{{ c.etiqueta }}</span>
              <div class="stepper">
                <button mat-icon-button type="button" [attr.aria-label]="'Restar ' + c.etiqueta" (click)="cambiar(c, -1)">
                  <mat-icon>remove</mat-icon>
                </button>
                <span class="valor">{{ valor(c.campo) }}{{ c.sufijo ?? '' }}</span>
                <button mat-icon-button type="button" [attr.aria-label]="'Sumar ' + c.etiqueta" (click)="cambiar(c, 1)">
                  <mat-icon>add</mat-icon>
                </button>
              </div>
            </div>
          }
        </div>
      }

      @if (ejercicio().observaciones) {
        <p class="nota">{{ ejercicio().observaciones }}</p>
      }
    </div>
  `,
  styles: `
    .tarjeta-ejercicio { border: 1px solid var(--mat-sys-outline-variant); border-radius: 12px; padding: 10px 14px; margin: 8px 0; }
    .titulo { display: flex; align-items: center; gap: 10px; }
    .orden { display: inline-grid; place-items: center; width: 24px; height: 24px; border-radius: 50%;
      background: var(--mat-sys-secondary-container); font: var(--mat-sys-label-medium); }
    .resumen { margin: 4px 0 0 34px; color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-medium); }
    .controles { display: flex; flex-wrap: wrap; gap: 8px 20px; margin: 8px 0 0 34px; }
    .control { display: flex; flex-direction: column; }
    .etiqueta { font: var(--mat-sys-label-small); color: var(--mat-sys-on-surface-variant); }
    .stepper { display: flex; align-items: center; }
    .valor { min-width: 52px; text-align: center; font: var(--mat-sys-title-small); }
    .nota { margin: 6px 0 0 34px; font: var(--mat-sys-body-small); color: var(--mat-sys-on-surface-variant); }
  `,
})
export class RutinaEjercicioCard {
  readonly ejercicio = input.required<RutinaEjercicio>();
  readonly editable = input(false);

  /** Etapa 9a-1: si el ejercicio se mide en segundos, el rango se rotula "Seg." y muestra la unidad. */
  controles(): Control[] {
    const seg = enSegundos(this.ejercicio().tipoMedida);
    return [
      { campo: 'seriesObjetivo', etiqueta: 'Series', paso: 1 },
      seg ? { campo: 'repeticionesMinimas', etiqueta: 'Seg. mín.', paso: 1, sufijo: ' s' }
          : { campo: 'repeticionesMinimas', etiqueta: 'Reps mín.', paso: 1 },
      seg ? { campo: 'repeticionesMaximas', etiqueta: 'Seg. máx.', paso: 1, sufijo: ' s' }
          : { campo: 'repeticionesMaximas', etiqueta: 'Reps máx.', paso: 1 },
      { campo: 'rirObjetivo', etiqueta: 'RIR', paso: 1 },
      { campo: 'descansoObjetivoSegundos', etiqueta: 'Descanso', paso: 15, sufijo: ' s' },
    ];
  }

  resumen(): string {
    return resumenEjercicio(this.ejercicio());
  }

  valor(campo: CampoRutina): number | string {
    const v = this.ejercicio()[campo];
    return v ?? '—';
  }

  cambiar(c: Control, sentido: 1 | -1): void {
    const e = this.ejercicio();
    Object.assign(e, aplicarPaso(e, c.campo, c.paso * sentido));
  }
}
