import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AccesoApi } from '../../core/api/acceso.api';
import { AjusteEjercicio, DecisionRevision, RutinaEjercicio, RutinaPendiente } from '../../core/models/acceso.models';
import { mensajeDeError } from '../../core/util/api-error';

/** Dueño y Personal Trainer: aprueban, editan o rechazan las rutinas generadas por la IA. */
@Component({
  selector: 'app-revisiones-pendientes',
  imports: [DatePipe, FormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, MatProgressBarModule],
  template: `
    <div class="encabezado">
      <div><h1>Rutinas para revisar</h1><p class="subtitulo">{{ rutinas().length }} pendientes</p></div>
    </div>

    @if (cargando()) {
      <mat-progress-bar mode="indeterminate" />
    } @else if (rutinas().length === 0) {
      <div class="estado"><mat-icon>task_alt</mat-icon><span>No hay rutinas pendientes de revisión.</span></div>
    } @else {
      @for (r of rutinas(); track r.id) {
        <article class="rutina">
          <header>
            <strong>{{ r.nombreSocio }}</strong>
            <span class="meta">{{ r.fechaAlta | date: 'dd/MM/yyyy HH:mm' }} · origen: {{ r.origenGeneracion || 'IA' }}</span>
          </header>
          @if (r.observaciones) { <p class="meta">{{ r.observaciones }}</p> }

          <table class="ejercicios">
            <thead><tr><th>Ejercicio</th><th>Series</th><th>Rep. mín</th><th>Rep. máx</th><th>RIR</th><th>Descanso (s)</th></tr></thead>
            <tbody>
              @for (e of r.ejercicios; track e.id) {
                <tr>
                  <td>{{ e.orden }}. {{ e.ejercicio }}</td>
                  @if (editando() === r.id) {
                    <td><input type="number" min="1" [(ngModel)]="e.seriesObjetivo" /></td>
                    <td><input type="number" min="1" [(ngModel)]="e.repeticionesMinimas" /></td>
                    <td><input type="number" min="1" [(ngModel)]="e.repeticionesMaximas" /></td>
                    <td><input type="number" min="0" max="5" [(ngModel)]="e.rirObjetivo" /></td>
                    <td><input type="number" min="1" [(ngModel)]="e.descansoObjetivoSegundos" /></td>
                  } @else {
                    <td>{{ e.seriesObjetivo }}</td><td>{{ e.repeticionesMinimas }}</td><td>{{ e.repeticionesMaximas }}</td>
                    <td>{{ e.rirObjetivo ?? '—' }}</td><td>{{ e.descansoObjetivoSegundos }}</td>
                  }
                </tr>
              }
            </tbody>
          </table>

          <mat-form-field class="comentario">
            <mat-label>Comentario {{ rechazando() === r.id ? '(obligatorio)' : '(opcional)' }}</mat-label>
            <input matInput [ngModel]="comentarios()[r.id] ?? ''" (ngModelChange)="setComentario(r.id, $event)" maxlength="500" />
          </mat-form-field>

          <footer>
            @if (editando() === r.id) {
              <button mat-button (click)="cancelarEdicion()">Cancelar</button>
              <button mat-flat-button [disabled]="procesando() === r.id" (click)="enviar(r, 'Editada')">Guardar cambios</button>
            } @else {
              <button mat-button (click)="rechazar(r)" [disabled]="procesando() === r.id">Rechazar</button>
              <button mat-button (click)="editando.set(r.id); rechazando.set(null)" [disabled]="procesando() === r.id">Editar</button>
              <button mat-flat-button (click)="enviar(r, 'Aprobada')" [disabled]="procesando() === r.id">Aprobar</button>
            }
          </footer>
        </article>
      }
    }
  `,
  styles: `
    .rutina { border: 1px solid var(--mat-sys-outline-variant); border-radius: 12px; padding: 16px; margin-bottom: 16px; }
    header { display: flex; flex-wrap: wrap; justify-content: space-between; gap: 8px; }
    .meta { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); margin: 4px 0; }
    .ejercicios { width: 100%; border-collapse: collapse; margin: 12px 0; overflow-x: auto; display: block; }
    th, td { text-align: left; padding: 6px 10px; white-space: nowrap; }
    th { font: var(--mat-sys-label-medium); color: var(--mat-sys-on-surface-variant); }
    input[type='number'] { width: 64px; }
    .comentario { width: 100%; }
    footer { display: flex; justify-content: flex-end; gap: 8px; }
  `,
})
export class RevisionesPendientes {
  private readonly api = inject(AccesoApi);
  private readonly snack = inject(MatSnackBar);

  readonly rutinas = signal<RutinaPendiente[]>([]);
  readonly cargando = signal(true);
  readonly procesando = signal<string | null>(null);
  readonly editando = signal<string | null>(null);
  readonly rechazando = signal<string | null>(null);
  readonly comentarios = signal<Record<string, string>>({});
  private copias = new Map<string, RutinaEjercicio[]>();

  constructor() {
    this.cargar();
  }

  cargar(): void {
    this.api.rutinasPendientes().subscribe({
      next: (r) => {
        this.rutinas.set(r);
        this.copias = new Map(r.map((x) => [x.id, x.ejercicios.map((e) => ({ ...e }))]));
        this.cargando.set(false);
      },
      error: (e) => { this.snack.open(mensajeDeError(e), 'Cerrar', { duration: 5000 }); this.cargando.set(false); },
    });
  }

  setComentario(id: string, texto: string): void {
    this.comentarios.update((c) => ({ ...c, [id]: texto }));
  }

  cancelarEdicion(): void {
    const id = this.editando();
    if (id) {
      // Restaura los valores originales descartando lo tipeado.
      this.rutinas.update((l) => l.map((r) => (r.id === id ? { ...r, ejercicios: this.copias.get(id)!.map((e) => ({ ...e })) } : r)));
    }
    this.editando.set(null);
  }

  rechazar(r: RutinaPendiente): void {
    // Primer clic: pide el motivo. Segundo clic con motivo cargado: envía.
    if (this.rechazando() !== r.id) {
      this.rechazando.set(r.id);
      this.snack.open('Escribí el motivo del rechazo y volvé a presionar Rechazar.', 'Cerrar', { duration: 4000 });
      return;
    }
    this.enviar(r, 'Rechazada');
  }

  enviar(r: RutinaPendiente, decision: DecisionRevision): void {
    const comentario = (this.comentarios()[r.id] ?? '').trim();
    if (decision === 'Rechazada' && !comentario) {
      this.snack.open('Indicá el motivo del rechazo.', 'Cerrar', { duration: 4000 });
      return;
    }

    const ajustes: AjusteEjercicio[] =
      decision === 'Editada' ? this.ajustesDe(r) : [];
    if (decision === 'Editada' && ajustes.length === 0) {
      this.snack.open('No modificaste ningún valor.', 'Cerrar', { duration: 3000 });
      return;
    }

    this.procesando.set(r.id);
    this.api.revisarRutina(r.id, { decision, comentario: comentario || undefined, ajustes }).subscribe({
      next: () => {
        this.rutinas.update((l) => l.filter((x) => x.id !== r.id));
        this.editando.set(null);
        this.rechazando.set(null);
        this.procesando.set(null);
        this.snack.open(`Rutina de ${r.nombreSocio}: ${decision.toLowerCase()}`, 'Cerrar', { duration: 4000 });
      },
      error: (e) => { this.snack.open(mensajeDeError(e), 'Cerrar', { duration: 6000 }); this.procesando.set(null); },
    });
  }

  /** Solo se envían los ejercicios cuyos valores cambiaron respecto de lo que generó la IA. */
  private ajustesDe(r: RutinaPendiente): AjusteEjercicio[] {
    const originales = this.copias.get(r.id) ?? [];
    const ajustes: AjusteEjercicio[] = [];
    for (const e of r.ejercicios) {
      const o = originales.find((x) => x.id === e.id);
      if (!o) continue;
      const cambio =
        e.seriesObjetivo !== o.seriesObjetivo || e.repeticionesMinimas !== o.repeticionesMinimas ||
        e.repeticionesMaximas !== o.repeticionesMaximas || e.rirObjetivo !== o.rirObjetivo ||
        e.descansoObjetivoSegundos !== o.descansoObjetivoSegundos;
      if (!cambio) continue;
      ajustes.push({
        prescripcionEjercicioId: e.id,
        seriesObjetivo: e.seriesObjetivo,
        repeticionesMinimas: e.repeticionesMinimas,
        repeticionesMaximas: e.repeticionesMaximas,
        rirObjetivo: e.rirObjetivo ?? undefined,
        descansoObjetivoSegundos: e.descansoObjetivoSegundos,
      });
    }
    return ajustes;
  }
}