import { HttpClient } from '@angular/common/http';
import { Component, Injectable, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../../core/api/api.config';
import { mensajeDeError } from '../../core/util/api-error';

/** Etapa 9b-2: contrato de POST /api/revisiones/{id}/lineas/{lineaId}/sustitutos. */
export interface SugerenciaSustitutos {
  lineaId: string;
  ejercicioActual: string;
  candidatos: { id: string; nombre: string }[];
  sugerencias: { ejercicioId: string; nombre: string; zonaObjetivo: string; equipamiento: string; justificacion: string }[];
  rechazadasPorLimites: { ejercicioId: string; codigos: string[] }[];
  respetaConjunto: boolean;
  descartada: boolean;
  motivoDescarte: string | null;
  modelo: string | null;
  latenciaMs: number | null;
}

@Injectable({ providedIn: 'root' })
export class SustitutosApi {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);

  sugerir(rutinaId: string, lineaId: string, motivo: string): Observable<SugerenciaSustitutos> {
    return this.http.post<SugerenciaSustitutos>(`${this.base}/revisiones/${rutinaId}/lineas/${lineaId}/sustitutos`, { motivo });
  }
}

const DESCARTES: Record<string, string> = {
  sin_candidatos: 'No hay ejercicios disponibles en el gimnasio que sirvan como candidatos.',
  salida_mal_formada: 'El modelo respondió en un formato inválido; la respuesta se descartó.',
  candidato_fuera_del_conjunto: 'El modelo propuso un ejercicio fuera de los candidatos recuperados; la respuesta se descartó.',
};

/**
 * Etapa 9b-2: sugerencia de sustitutos para el Dueño o el Personal Trainer en la pantalla de revisión. Es solo una
 * sugerencia del modelo local: no modifica la rutina y nunca se muestra al Socio. Aplicarla queda pendiente de una
 * decisión de diseño (ver el informe de la etapa).
 */
@Component({
  selector: 'app-sustitutos-panel',
  imports: [FormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, MatProgressBarModule],
  template: `
    @if (!abierto()) {
      <button mat-button type="button" class="abrir" (click)="abierto.set(true)"><mat-icon>auto_awesome</mat-icon> Sugerir sustitutos</button>
    } @else {
      <div class="panel">
        <div class="fila">
          <mat-form-field class="motivo">
            <mat-label>Motivo de la sustitución</mat-label>
            <input matInput [(ngModel)]="motivo" maxlength="300" />
          </mat-form-field>
          <button mat-stroked-button type="button" [disabled]="pidiendo() || motivo.trim().length < 3" (click)="pedir()">Pedir sugerencia</button>
          <button mat-button type="button" (click)="cerrar()">Cerrar</button>
        </div>
        @if (pidiendo()) { <mat-progress-bar mode="indeterminate" /> }
        @if (error()) { <p class="aviso">{{ error() }}</p> }
        @if (resultado(); as r) {
          <p class="nota"><mat-icon>info</mat-icon> Sugerencia de un modelo de lenguaje local, elegida solo entre ejercicios disponibles en el gimnasio
            y verificada contra los límites de la rutina. No se aplica sola: revisala antes de usarla.</p>
          @if (r.descartada) {
            <p class="aviso">{{ descarte(r.motivoDescarte) }}</p>
          } @else {
            @for (s of r.sugerencias; track s.ejercicioId) {
              <div class="sugerencia"><strong>{{ s.nombre }}</strong><span>{{ s.justificacion }}</span></div>
            } @empty {
              <p class="meta">El modelo no encontró un sustituto adecuado entre los candidatos.</p>
            }
            @if (r.rechazadasPorLimites.length > 0) {
              <p class="meta">{{ r.rechazadasPorLimites.length }} sugerencia(s) descartada(s) por los límites de la rutina.</p>
            }
          }
        }
      </div>
    }
  `,
  styles: `
    .panel { border-left: 3px solid var(--mat-sys-primary); padding: 4px 12px; margin: 4px 0 12px; }
    .fila { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
    .motivo { flex: 1; min-width: 200px; }
    .nota, .meta { display: flex; gap: 6px; align-items: center; color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); }
    .aviso { color: var(--mat-sys-error); font: var(--mat-sys-body-small); }
    .sugerencia { display: grid; gap: 2px; padding: 6px 0; border-top: 1px solid var(--mat-sys-outline-variant); }
    .sugerencia span { font: var(--mat-sys-body-small); }
  `,
})
export class SustitutosPanel {
  private readonly api = inject(SustitutosApi);

  readonly rutinaId = input.required<string>();
  readonly lineaId = input.required<string>();

  readonly abierto = signal(false);
  readonly pidiendo = signal(false);
  readonly error = signal<string | null>(null);
  readonly resultado = signal<SugerenciaSustitutos | null>(null);
  motivo = '';

  descarte(motivo: string | null): string {
    return (motivo && DESCARTES[motivo]) || 'La respuesta del modelo se descartó.';
  }

  cerrar(): void {
    this.abierto.set(false);
    this.resultado.set(null);
    this.error.set(null);
  }

  pedir(): void {
    this.pidiendo.set(true);
    this.error.set(null);
    this.api.sugerir(this.rutinaId(), this.lineaId(), this.motivo.trim()).subscribe({
      next: (r) => { this.resultado.set(r); this.pidiendo.set(false); },
      error: (e) => { this.error.set(mensajeDeError(e, 'No se pudo obtener la sugerencia.')); this.pidiendo.set(false); },
    });
  }
}
