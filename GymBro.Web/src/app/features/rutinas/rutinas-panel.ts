import { DatePipe } from '@angular/common';
import { Component, effect, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { PrescripcionesApi } from '../../core/api/prescripciones.api';
import { RutinaPendiente } from '../../core/models/acceso.models';
import { mensajeDeError } from '../../core/util/api-error';
import { etiquetaEstado } from '../../core/util/rutina-formato';
import { RutinaEjercicioCard } from './rutina-ejercicio-card';

/**
 * Pestaña "Rutinas" del detalle de un socio: historial de prescripciones y generación de la rutina baseline.
 * Si el gimnasio exige revisión, la nueva rutina queda pendiente y se revisa en "Rutinas para revisar".
 */
@Component({
  selector: 'app-rutinas-panel',
  imports: [DatePipe, MatButtonModule, MatIconModule, MatProgressBarModule, RutinaEjercicioCard],
  template: `
    <section class="tarjeta acciones">
      <div>
        <h2>Rutina baseline</h2>
        <p class="meta">
          Genera una rutina de 4 a 6 ejercicios (pecho, espalda, piernas, brazos y core) a partir del perfil y la
          última medición del socio. No usa IA: aplica reglas fijas sobre el catálogo.
        </p>
      </div>
      <button mat-flat-button type="button" [disabled]="generando()" (click)="generar()">
        <mat-icon>auto_fix_high</mat-icon> Generar rutina
      </button>
    </section>

    @if (cargando()) {
      <mat-progress-bar mode="indeterminate" />
    } @else if (rutinas().length === 0) {
      <div class="estado"><mat-icon>fitness_center</mat-icon><span>El socio todavía no tiene rutinas.</span></div>
    } @else {
      @for (r of rutinas(); track r.id) {
        <article class="rutina">
          <header>
            <strong>{{ etiqueta(r.estado) }}</strong>
            <span class="meta">{{ r.fechaAlta | date: 'dd/MM/yyyy HH:mm' }} · origen: {{ r.origenGeneracion || '—' }}</span>
          </header>
          @if (r.observaciones) { <p class="meta">{{ r.observaciones }}</p> }
          @for (e of r.ejercicios; track e.id) {
            <app-rutina-ejercicio-card [ejercicio]="e" />
          }
        </article>
      }
    }
  `,
  styles: `
    .acciones { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 12px; margin: 16px 0; }
    .meta { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); margin: 4px 0; }
    .rutina { border: 1px solid var(--mat-sys-outline-variant); border-radius: 12px; padding: 16px; margin-bottom: 16px; }
    header { display: flex; flex-wrap: wrap; justify-content: space-between; gap: 8px; }
  `,
})
export class RutinasPanel {
  private readonly api = inject(PrescripcionesApi);
  private readonly snack = inject(MatSnackBar);

  readonly socioId = input.required<string>();
  readonly rutinas = signal<RutinaPendiente[]>([]);
  readonly cargando = signal(true);
  readonly generando = signal(false);
  readonly etiqueta = etiquetaEstado;

  constructor() {
    effect(() => {
      this.cargar(this.socioId());
    });
  }

  private cargar(socioId: string): void {
    this.cargando.set(true);
    this.api.listar(socioId).subscribe({
      next: (r) => { this.rutinas.set(r); this.cargando.set(false); },
      error: (e) => { this.snack.open(mensajeDeError(e), 'Cerrar', { duration: 6000 }); this.cargando.set(false); },
    });
  }

  generar(): void {
    this.generando.set(true);
    this.api.generarBaseline(this.socioId()).subscribe({
      next: (nueva) => {
        this.rutinas.update((l) => [nueva, ...l]);
        this.generando.set(false);
        this.snack.open(
          nueva.estado === 'PendienteRevision'
            ? 'Rutina generada: queda pendiente de revisión.'
            : 'Rutina generada y aprobada.',
          'Cerrar', { duration: 5000 });
      },
      error: (e) => { this.snack.open(mensajeDeError(e), 'Cerrar', { duration: 7000 }); this.generando.set(false); },
    });
  }
}
