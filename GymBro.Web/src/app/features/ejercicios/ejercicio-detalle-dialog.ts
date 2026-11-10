import { Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { EjerciciosApi } from '../../core/api/ejercicios.api';
import { EjercicioDetalle } from '../../core/models/ejercicio.models';
import { mensajeDeError } from '../../core/util/api-error';

@Component({
  selector: 'app-ejercicio-detalle-dialog',
  imports: [MatDialogModule, MatButtonModule, MatIconModule, MatProgressBarModule],
  template: `
    @if (ejercicio(); as e) {
      <h2 mat-dialog-title>{{ e.nombre }}</h2>
      <mat-dialog-content>
        <dl class="datos">
          <div><dt>Zona objetivo</dt><dd>{{ e.zonaObjetivo || '—' }}</dd></div>
          <div><dt>Músculo principal</dt><dd>{{ e.musculoPrincipal || '—' }}</dd></div>
          <div><dt>Equipamiento</dt><dd>{{ e.equipamiento || '—' }}</dd></div>
          <div><dt>Dificultad</dt><dd>{{ e.dificultad || '—' }}</dd></div>
          <div class="ancho"><dt>Sinergistas</dt><dd>{{ e.sinergistas || '—' }}</dd></div>
          <div class="ancho"><dt>Estabilizadores</dt><dd>{{ e.estabilizadores || '—' }}</dd></div>
        </dl>
        @if (e.preparacion) {
          <h3>Preparación</h3>
          <p class="texto">{{ e.preparacion }}</p>
        }
        @if (e.ejecucion) {
          <h3>Ejecución</h3>
          <p class="texto">{{ e.ejecucion }}</p>
        }
      </mat-dialog-content>
    } @else {
      <mat-dialog-content>
        @if (error()) {
          <div class="estado error"><mat-icon>error_outline</mat-icon><span>{{ error() }}</span></div>
        } @else {
          <mat-progress-bar mode="indeterminate" />
        }
      </mat-dialog-content>
    }
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cerrar</button>
    </mat-dialog-actions>
  `,
  styles: `
    .datos {
      display: grid; grid-template-columns: repeat(2, 1fr); gap: 12px; margin: 0 0 8px;
      dt { font: var(--mat-sys-label-medium); color: var(--mat-sys-on-surface-variant); }
      dd { margin: 2px 0 0; }
      .ancho { grid-column: 1 / -1; }
    }
    h3 { font: var(--mat-sys-title-small); margin: 16px 0 4px; }
    .texto { white-space: pre-line; margin: 0; }
  `,
})
export class EjercicioDetalleDialog {
  readonly ejercicio = signal<EjercicioDetalle | null>(null);
  readonly error = signal<string | null>(null);

  constructor() {
    const id = inject<string>(MAT_DIALOG_DATA);
    inject(EjerciciosApi)
      .obtener(id)
      .subscribe({
        next: (e) => this.ejercicio.set(e),
        error: (e) => this.error.set(mensajeDeError(e, 'No se pudo cargar el ejercicio.')),
      });
  }
}
