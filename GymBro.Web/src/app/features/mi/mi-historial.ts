import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { EjerciciosApi } from '../../core/api/ejercicios.api';
import { SesionesApi } from '../../core/api/sesiones.api';
import { AuthService } from '../../core/auth/auth.service';
import { ComparacionSesion, Sesion } from '../../core/models/sesion.models';
import { mensajeDeError } from '../../core/util/api-error';

/** Historial del Socio: sesiones registradas, de la más reciente a la más antigua. */
@Component({
  selector: 'app-mi-historial',
  imports: [DatePipe, RouterLink, MatIconModule, MatProgressBarModule],
  template: `
    <h1 class="titulo">Historial</h1>
    @if (cargando()) {
      <mat-progress-bar mode="indeterminate" />
    } @else if (error()) {
      <div class="aviso">{{ error() }}</div>
    } @else {
      @for (s of ordenadas(); track s.id) {
        <a class="item" [routerLink]="['/mi/historial', s.id]">
          <div>
            <strong>{{ s.inicio | date: 'EEE dd/MM/yyyy HH:mm' }}</strong>
            <p class="meta">{{ s.ejercicios.length }} ejercicios · {{ series(s) }} series ·
              {{ s.prescripcionEntrenamientoId ? 'con rutina' : 'libre' }}</p>
          </div>
          <mat-icon>chevron_right</mat-icon>
        </a>
      } @empty {
        <div class="estado"><mat-icon>history</mat-icon><span>Todavía no registraste sesiones.</span></div>
      }
    }
  `,
  styles: `
    .titulo { font: var(--mat-sys-headline-small); margin: 4px 0 12px; }
    .item { display: flex; align-items: center; justify-content: space-between; gap: 8px; padding: 12px;
      border: 1px solid var(--mat-sys-outline-variant); border-radius: 12px; margin-bottom: 8px; color: inherit; text-decoration: none; }
    .meta { margin: 2px 0 0; color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); }
  `,
})
export class MiHistorial {
  private readonly api = inject(SesionesApi);
  private readonly socioId = inject(AuthService).sesion()?.socioId ?? null;

  readonly sesiones = signal<Sesion[]>([]);
  readonly cargando = signal(true);
  readonly error = signal<string | null>(null);
  readonly ordenadas = computed(() => [...this.sesiones()].sort((a, b) => b.inicio.localeCompare(a.inicio)));

  constructor() {
    if (!this.socioId) {
      this.error.set('Tu cuenta no tiene una ficha de socio asociada.');
      this.cargando.set(false);
      return;
    }
    this.api.listarPorSocio(this.socioId).subscribe({
      next: (s) => { this.sesiones.set(s); this.cargando.set(false); },
      error: (e) => { this.error.set(mensajeDeError(e)); this.cargando.set(false); },
    });
  }

  series(s: Sesion): number {
    return s.ejercicios.reduce((n, e) => n + e.seriesRealizadas.filter((x) => !x.esCalentamiento).length, 0);
  }
}

/** Detalle de una sesión del Socio: comparación prescripción vs. ejecución (GET /api/Sesiones/{id}/comparacion). */
@Component({
  selector: 'app-mi-sesion-detalle',
  imports: [DatePipe, DecimalPipe, RouterLink, MatButtonModule, MatIconModule, MatProgressBarModule],
  template: `
    <a mat-button routerLink="/mi/historial"><mat-icon>arrow_back</mat-icon> Historial</a>
    @if (cargando()) {
      <mat-progress-bar mode="indeterminate" />
    } @else if (error()) {
      <div class="aviso">{{ error() }}</div>
    } @else if (sesion(); as s) {
      <h1 class="titulo">{{ s.inicio | date: 'EEE dd/MM/yyyy HH:mm' }}</h1>
      <p class="meta">{{ s.prescripcionEntrenamientoId ? 'Sesión de tu rutina: objetivo vs. lo que hiciste.' : 'Sesión libre (sin rutina).' }}</p>

      @for (e of comparacion()?.ejercicios ?? []; track e.ejercicioId + e.orden) {
        <section class="ejercicio">
          <h2>{{ e.orden }}. {{ nombres()[e.ejercicioId] ?? 'Ejercicio' }}</h2>
          @if (e.prescripcion; as p) {
            <p class="meta">Objetivo: {{ p.seriesObjetivo }} × {{ p.repeticionesMinimas }}–{{ p.repeticionesMaximas }} reps
              @if (p.rirObjetivo !== null) { · RIR {{ p.rirObjetivo }} } · {{ p.descansoObjetivoSegundos }} s</p>
            <p class="resultado" [class.ok]="e.comparacion.series.diferencia === 0">{{ e.comparacion.series.mensaje }}</p>
          }
          @if (e.ejecucion; as x) {
            @for (d of x.detallesSeries; track d.ordenSerie) {
              <div class="serie">
                <span>{{ d.ordenSerie }}</span>
                <span>{{ d.pesoLevantadoKg | number: '1.0-1' }} kg × {{ d.repeticiones }}@if (d.rir !== null) { · RIR {{ d.rir }} }</span>
                @if (d.repeticionesComparacion; as c) {
                  <mat-icon [class.ok]="c.cumpleObjetivo" [attr.aria-label]="c.mensaje">{{ c.cumpleObjetivo ? 'check_circle' : 'error' }}</mat-icon>
                }
              </div>
            }
            <p class="meta">Volumen efectivo: {{ x.volumenEfectivoKg | number: '1.0-1' }} kg</p>
          } @else {
            <p class="meta">No registraste series de este ejercicio.</p>
          }
        </section>
      }
    }
  `,
  styles: `
    .titulo { font: var(--mat-sys-headline-small); margin: 8px 0 4px; text-transform: capitalize; }
    .meta { margin: 2px 0; color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); }
    .ejercicio { border: 1px solid var(--mat-sys-outline-variant); border-radius: 16px; padding: 12px; margin: 12px 0; }
    .ejercicio h2 { margin: 0 0 4px; font: var(--mat-sys-title-medium); }
    .resultado { margin: 4px 0 8px; font: var(--mat-sys-label-large); color: var(--mat-sys-error); }
    .resultado.ok, mat-icon.ok { color: var(--mat-sys-primary); }
    .serie { display: grid; grid-template-columns: 24px 1fr 24px; gap: 8px; align-items: center; padding: 4px 0; font: var(--mat-sys-body-medium); }
    mat-icon { color: var(--mat-sys-error); font-size: 20px; width: 20px; height: 20px; }
  `,
})
export class MiSesionDetalle {
  private readonly api = inject(SesionesApi);
  private readonly ejercicios = inject(EjerciciosApi);

  readonly id = input.required<string>();
  readonly sesion = signal<Sesion | null>(null);
  readonly comparacion = signal<ComparacionSesion | null>(null);
  readonly nombres = signal<Record<string, string>>({});
  readonly cargando = signal(true);
  readonly error = signal<string | null>(null);

  constructor() {
    effect(() => this.cargar(this.id()));
  }

  private cargar(id: string): void {
    this.cargando.set(true);
    forkJoin({ sesion: this.api.obtener(id), comparacion: this.api.comparacion(id) }).subscribe({
      next: ({ sesion, comparacion }) => {
        this.sesion.set(sesion);
        this.comparacion.set(comparacion);
        this.cargando.set(false);
        const ids = [...new Set(comparacion.ejercicios.map((e) => e.ejercicioId))];
        if (ids.length) this.ejercicios.nombres(ids).subscribe({ next: (n) => this.nombres.set(n), error: () => undefined });
      },
      error: (e) => { this.error.set(mensajeDeError(e)); this.cargando.set(false); },
    });
  }
}
