import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { PrescripcionesApi } from '../../core/api/prescripciones.api';
import { AuthService } from '../../core/auth/auth.service';
import { RutinaPendiente } from '../../core/models/acceso.models';
import { mensajeDeError } from '../../core/util/api-error';
import { etiquetaEstado } from '../../core/util/rutina-formato';
import { RutinaEjercicioCard } from '../rutinas/rutina-ejercicio-card';
import { rutinaVigente } from './entrenamiento';

/** Mi rutina: la rutina vigente, una tarjeta de una línea por ejercicio (solo lectura). */
@Component({
  selector: 'app-mi-rutina',
  imports: [DatePipe, RouterLink, MatButtonModule, MatIconModule, MatProgressBarModule, RutinaEjercicioCard],
  template: `
    <h1 class="titulo">Mi rutina</h1>
    @if (cargando()) {
      <mat-progress-bar mode="indeterminate" />
    } @else if (error()) {
      <div class="aviso">{{ error() }}</div>
    } @else if (vigente(); as r) {
      <p class="meta">{{ etiqueta(r.estado) }} · desde el {{ r.fechaAlta | date: 'dd/MM/yyyy' }}</p>
      @if (r.observaciones) { <p class="meta">{{ r.observaciones }}</p> }
      @for (e of r.ejercicios; track e.id) {
        <app-rutina-ejercicio-card [ejercicio]="e" />
      }
      <a mat-flat-button class="principal" routerLink="/mi/entrenar"><mat-icon>play_arrow</mat-icon> Entrenar</a>
    } @else {
      <div class="estado">
        <mat-icon>fitness_center</mat-icon>
        <span>No tenés una rutina vigente. Podés generarla desde <a routerLink="/mi">Hoy</a>.</span>
      </div>
    }
  `,
  styles: `
    .titulo { font: var(--mat-sys-headline-small); margin: 4px 0 8px; }
    .meta { margin: 4px 0; color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-medium); }
    .principal { width: 100%; height: 48px; margin-top: 12px; }
  `,
})
export class MiRutina {
  private readonly api = inject(PrescripcionesApi);
  private readonly socioId = inject(AuthService).sesion()?.socioId ?? null;

  readonly rutinas = signal<RutinaPendiente[]>([]);
  readonly cargando = signal(true);
  readonly error = signal<string | null>(null);
  readonly vigente = computed(() => rutinaVigente(this.rutinas()));
  readonly etiqueta = etiquetaEstado;

  constructor() {
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
}
