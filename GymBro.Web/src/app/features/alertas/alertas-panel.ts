import { DatePipe } from '@angular/common';
import { Component, effect, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AlertasApi } from '../../core/api/alertas.api';
import { AlertaSeguridad, REGLA_ETIQUETA, resumenDetalle } from '../../core/models/alertas.models';
import { mensajeDeError } from '../../core/util/api-error';

/**
 * Etapa 10a: alertas de seguridad de un socio, en su ficha (Dueño y Personal Trainer). Muestra la regla, los valores y
 * permite marcarlas como vistas. Son reglas deterministas con umbrales provisionales: orientan, no diagnostican.
 */
@Component({
  selector: 'app-alertas-panel',
  imports: [DatePipe, MatButtonModule, MatIconModule, MatProgressBarModule],
  template: `
    @if (cargando()) {
      <mat-progress-bar mode="indeterminate" />
    } @else if (error()) {
      <div class="estado error"><mat-icon>error_outline</mat-icon><span>{{ error() }}</span></div>
    } @else if (alertas().length === 0) {
      <div class="estado"><mat-icon>verified</mat-icon><span>Sin alertas de seguridad.</span></div>
    } @else {
      <ul class="lista">
        @for (a of alertas(); track a.id) {
          <li class="alerta" [class.pendiente]="a.estado === 'Pendiente'" [attr.data-regla]="a.reglaCodigo">
            <div class="cuerpo">
              <strong>{{ etiqueta[a.reglaCodigo] }}</strong> <span class="codigo">{{ a.reglaCodigo }}</span>
              <div>{{ a.mensaje }}</div>
              <div class="valores">{{ resumen(a) }}</div>
              <div class="meta">{{ a.fechaAlta | date: 'dd/MM/yyyy HH:mm' }} · {{ a.estado === 'Vista' ? 'Vista' : 'Pendiente' }}</div>
            </div>
            @if (a.estado === 'Pendiente') {
              <button mat-stroked-button class="marcar-vista" [disabled]="procesando() === a.id" (click)="marcarVista(a)">Marcar vista</button>
            }
          </li>
        }
      </ul>
    }
  `,
  styles: `
    .lista { list-style: none; padding: 0; margin: 8px 0; display: grid; gap: 8px; }
    .alerta { display: flex; justify-content: space-between; align-items: center; gap: 12px; flex-wrap: wrap;
      border: 1px solid var(--mat-sys-outline-variant); border-radius: 12px; padding: 12px; }
    .alerta.pendiente { border-color: var(--mat-sys-tertiary); }
    .cuerpo { flex: 1 1 260px; display: grid; gap: 2px; }
    .codigo, .meta, .valores { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); }
  `,
})
export class AlertasPanel {
  private readonly api = inject(AlertasApi);
  private readonly snack = inject(MatSnackBar);

  readonly socioId = input.required<string>();
  readonly alertas = signal<AlertaSeguridad[]>([]);
  readonly cargando = signal(true);
  readonly error = signal<string | null>(null);
  readonly procesando = signal<string | null>(null);
  readonly etiqueta = REGLA_ETIQUETA;
  readonly resumen = resumenDetalle;

  constructor() {
    effect(() => {
      const id = this.socioId();
      this.cargando.set(true);
      this.api.listar(id).subscribe({
        next: (l) => { this.alertas.set(l); this.cargando.set(false); },
        error: (e) => { this.error.set(mensajeDeError(e, 'No se pudieron cargar las alertas.')); this.cargando.set(false); },
      });
    });
  }

  marcarVista(a: AlertaSeguridad): void {
    this.procesando.set(a.id);
    this.api.marcarVista(a.id).subscribe({
      next: (vista) => {
        this.alertas.update((l) => l.map((x) => (x.id === a.id ? vista : x)));
        this.procesando.set(null);
      },
      error: (e) => { this.snack.open(mensajeDeError(e), 'Cerrar', { duration: 5000 }); this.procesando.set(null); },
    });
  }
}
