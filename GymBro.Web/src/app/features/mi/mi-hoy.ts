import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { RouterLink } from '@angular/router';
import { BienestarApi } from '../../core/api/bienestar.api';
import { PrescripcionesApi } from '../../core/api/prescripciones.api';
import { AuthService } from '../../core/auth/auth.service';
import { RutinaPendiente } from '../../core/models/acceso.models';
import { mensajeDeError } from '../../core/util/api-error';
import { MisAvisos } from '../alertas/mis-avisos';
import { leerBorrador } from '../../core/util/borrador-sesion';
import { etiquetaEstado } from '../../core/util/rutina-formato';
import { borradorValido, Entrenamiento, rutinaEnRevision, rutinaVigente } from './entrenamiento';

/** Inicio del Socio: rutina vigente y acceso a entrenar; si no tiene rutina, la puede generar. */
@Component({
  selector: 'app-mi-hoy',
  imports: [DatePipe, RouterLink, MatButtonModule, MatIconModule, MatProgressBarModule, MisAvisos],
  template: `
    <h1 class="titulo">Hoy</h1>

    <!-- Etapa 10a: avisos de las reglas de seguridad y sugerencias de carga. -->
    <app-mis-avisos />

    <!-- Etapa 8b: registro de bienestar del día. -->
    <section class="tarjeta-mi bienestar">
      <h2>¿Cómo estás hoy?</h2>
      <p class="meta">
        {{ bienestarCargado() ? 'Ya cargaste tu registro de hoy. Podés corregirlo.' : 'Sueño, estrés, fatiga y comida: menos de 30 segundos.' }}
      </p>
      <a mat-stroked-button class="principal" routerLink="/mi/bienestar">
        <mat-icon>{{ bienestarCargado() ? 'edit' : 'favorite' }}</mat-icon> {{ bienestarCargado() ? 'Editar' : 'Cargar' }}
      </a>
    </section>

    @if (cargando()) {
      <mat-progress-bar mode="indeterminate" />
    } @else if (error()) {
      <div class="aviso">{{ error() }}</div>
    } @else {
      @if (vigente(); as r) {
        <section class="tarjeta-mi">
          <p class="etiqueta">Tu rutina · {{ etiqueta(r.estado) }}</p>
          <h2>{{ r.ejercicios.length }} ejercicios</h2>
          <p class="meta">Desde el {{ r.fechaAlta | date: 'dd/MM/yyyy' }}</p>
          <a mat-flat-button class="principal" routerLink="/mi/entrenar">
            <mat-icon>play_arrow</mat-icon> {{ hayBorrador() ? 'Continuar entrenamiento' : 'Empezar entrenamiento' }}
          </a>
          <a mat-button routerLink="/mi/rutina">Ver la rutina</a>
        </section>
      }

      @if (enRevision(); as p) {
        <section class="aviso">
          <mat-icon>hourglass_top</mat-icon>
          <span>
            {{ vigente() ? 'Tenés una rutina nueva' : 'Tu rutina' }} pendiente de revisión por tu entrenador
            (generada el {{ p.fechaAlta | date: 'dd/MM/yyyy' }}).
          </span>
        </section>
      }

      @if (!vigente() && !enRevision()) {
        <section class="tarjeta-mi">
          <h2>Todavía no tenés una rutina</h2>
          <p class="meta">
            GymBro arma una rutina inicial con tu perfil y tu última medición. Necesitás haber dado el consentimiento
            de datos de salud y tener al menos una medición (peso y altura).
          </p>
          <button mat-flat-button class="principal" [disabled]="generando()" (click)="generar()">
            <mat-icon>auto_fix_high</mat-icon> Generar mi rutina
          </button>
          <a mat-button routerLink="/mi/mediciones">Cargar una medición</a>
        </section>
      }
    }
  `,
  styles: `
    .titulo { font: var(--mat-sys-headline-small); margin: 4px 0 16px; }
    .tarjeta-mi { border: 1px solid var(--mat-sys-outline-variant); border-radius: 16px; padding: 16px; margin-bottom: 16px;
      display: flex; flex-direction: column; align-items: flex-start; gap: 8px; }
    .tarjeta-mi h2 { margin: 0; font: var(--mat-sys-title-large); }
    .etiqueta { margin: 0; font: var(--mat-sys-label-large); color: var(--mat-sys-primary); }
    .meta { margin: 0; color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-medium); }
    .principal { width: 100%; height: 48px; margin-top: 8px; }
    .aviso { display: flex; gap: 8px; align-items: flex-start; }
  `,
})
export class MiHoy {
  private readonly api = inject(PrescripcionesApi);
  private readonly auth = inject(AuthService);
  private readonly snack = inject(MatSnackBar);

  private readonly socioId = this.auth.sesion()?.socioId ?? null;
  readonly rutinas = signal<RutinaPendiente[]>([]);
  readonly cargando = signal(true);
  readonly generando = signal(false);
  readonly error = signal<string | null>(null);
  readonly vigente = computed(() => rutinaVigente(this.rutinas()));
  readonly enRevision = computed(() => rutinaEnRevision(this.rutinas()));
  readonly hayBorrador = computed(() => {
    const v = this.vigente();
    return !!v && !!this.socioId && borradorValido(leerBorrador<Entrenamiento>(this.socioId), this.socioId, v.id);
  });
  readonly etiqueta = etiquetaEstado;
  /** Si ya hay registro de bienestar de hoy (si la consulta falla, la tarjeta ofrece cargarlo igual). */
  readonly bienestarCargado = signal(false);

  constructor() {
    inject(BienestarApi).hoy().subscribe({
      next: (h) => this.bienestarCargado.set(h.registro !== null),
      error: () => this.bienestarCargado.set(false),
    });

    if (!this.socioId) {
      this.error.set('Tu cuenta no tiene una ficha de socio asociada.');
      this.cargando.set(false);
      return;
    }
    this.api.listar(this.socioId).subscribe({
      next: (r) => { this.rutinas.set(r); this.cargando.set(false); },
      error: (e) => { this.error.set(mensajeDeError(e)); this.cargando.set(false); },
    });
  }

  generar(): void {
    if (!this.socioId) return;
    this.generando.set(true);
    this.api.generarBaseline(this.socioId).subscribe({
      next: (nueva) => {
        this.rutinas.update((l) => [nueva, ...l]);
        this.generando.set(false);
        this.snack.open(
          nueva.estado === 'PendienteRevision' ? 'Rutina generada: tu entrenador la va a revisar.' : 'Rutina generada. ¡A entrenar!',
          'Cerrar', { duration: 5000 });
      },
      // Sin medición o sin consentimiento, la API responde 400 con el motivo.
      error: (e) => { this.snack.open(mensajeDeError(e), 'Cerrar', { duration: 7000 }); this.generando.set(false); },
    });
  }
}
