import { DatePipe } from '@angular/common';
import { Component, effect, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { PrescripcionesApi } from '../../core/api/prescripciones.api';
import { NivelLimitacion, RutinaPendiente } from '../../core/models/acceso.models';
import { mensajeDeError } from '../../core/util/api-error';
import { etiquetaEstado } from '../../core/util/rutina-formato';
import { RutinaEjercicioCard } from './rutina-ejercicio-card';

/**
 * Pestaña "Rutinas" del detalle de un socio: historial de prescripciones, generación de la rutina baseline y, desde la
 * Etapa 7d, limitaciones articulares y generación de la rutina del protocolo de piernas (sin recargar la pantalla).
 * Si el gimnasio exige revisión, la nueva rutina queda pendiente y se revisa en "Rutinas para revisar".
 */
@Component({
  selector: 'app-rutinas-panel',
  imports: [DatePipe, MatButtonModule, MatFormFieldModule, MatIconModule, MatProgressBarModule, MatSelectModule, RutinaEjercicioCard],
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

    <section class="tarjeta piernas">
      <div>
        <h2>Protocolo de piernas</h2>
        <p class="meta">
          Genera una rutina de extremidad inferior con los ejercicios curados del protocolo, según el perfil, el historial,
          las alertas y las limitaciones articulares del socio. No usa IA. Los parámetros son provisionales y los revisa la
          Personal Trainer. Requiere el consentimiento de datos de salud y al menos una medición.
        </p>
      </div>
      <div class="limitaciones">
        <mat-form-field appearance="outline">
          <mat-label>Rodilla</mat-label>
          <mat-select [value]="rodilla()" (selectionChange)="rodilla.set($event.value)" aria-label="Limitación de rodilla">
            @for (o of niveles; track o.valor) { <mat-option [value]="o.valor">{{ o.etiqueta }}</mat-option> }
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Zona lumbar</mat-label>
          <mat-select [value]="lumbar()" (selectionChange)="lumbar.set($event.value)" aria-label="Limitación lumbar">
            @for (o of niveles; track o.valor) { <mat-option [value]="o.valor">{{ o.etiqueta }}</mat-option> }
          </mat-select>
        </mat-form-field>
        <button mat-stroked-button type="button" class="guardar-limitaciones" [disabled]="guardandoLimitaciones()" (click)="guardarLimitaciones()">
          <mat-icon>save</mat-icon> Guardar limitaciones
        </button>
      </div>
      <button mat-flat-button type="button" class="generar-piernas" [disabled]="generandoPiernas()" (click)="generarPiernas()">
        <mat-icon>directions_run</mat-icon> Generar rutina de piernas
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
            <span class="meta">{{ r.fechaAlta | date: 'dd/MM/yyyy HH:mm' }} · origen: {{ r.origenGeneracion || '—' }}@if (r.protocoloVersion) { · {{ r.protocoloVersion }}, generación {{ r.numeroGeneracion }}}</span>
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
    .piernas { display: flex; flex-direction: column; gap: 8px; margin: 16px 0; }
    .limitaciones { display: flex; flex-wrap: wrap; align-items: center; gap: 8px 16px; }
    .generar-piernas { align-self: flex-start; }
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

  // Etapa 7d: protocolo de piernas.
  readonly niveles: { valor: NivelLimitacion; etiqueta: string }[] = [
    { valor: 'SinLimitacion', etiqueta: 'Sin limitación' },
    { valor: 'Moderada', etiqueta: 'Moderada' },
    { valor: 'Alta', etiqueta: 'Alta' },
  ];
  readonly rodilla = signal<NivelLimitacion>('SinLimitacion');
  readonly lumbar = signal<NivelLimitacion>('SinLimitacion');
  readonly guardandoLimitaciones = signal(false);
  readonly generandoPiernas = signal(false);

  constructor() {
    effect(() => {
      const socioId = this.socioId();
      this.cargar(socioId);
      this.cargarLimitaciones(socioId);
    });
  }

  private cargarLimitaciones(socioId: string): void {
    this.api.obtenerLimitaciones(socioId).subscribe({
      next: (l) => { this.rodilla.set(l.rodilla); this.lumbar.set(l.lumbar); },
      error: (e) => this.snack.open(mensajeDeError(e), 'Cerrar', { duration: 6000 }),
    });
  }

  guardarLimitaciones(): void {
    this.guardandoLimitaciones.set(true);
    this.api.guardarLimitaciones(this.socioId(), { rodilla: this.rodilla(), lumbar: this.lumbar() }).subscribe({
      next: (l) => {
        this.rodilla.set(l.rodilla);
        this.lumbar.set(l.lumbar);
        this.guardandoLimitaciones.set(false);
        this.snack.open('Limitaciones guardadas.', 'Cerrar', { duration: 4000 });
      },
      error: (e) => { this.snack.open(mensajeDeError(e), 'Cerrar', { duration: 7000 }); this.guardandoLimitaciones.set(false); },
    });
  }

  generarPiernas(): void {
    this.generandoPiernas.set(true);
    this.api.generarPiernas(this.socioId()).subscribe({
      next: (nueva) => {
        this.rutinas.update((l) => [nueva, ...l]);
        this.generandoPiernas.set(false);
        this.snack.open(
          nueva.estado === 'PendienteRevision'
            ? 'Rutina de piernas generada: queda pendiente de revisión.'
            : 'Rutina de piernas generada y aprobada.',
          'Cerrar', { duration: 5000 });
      },
      error: (e) => { this.snack.open(mensajeDeError(e), 'Cerrar', { duration: 8000 }); this.generandoPiernas.set(false); },
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
