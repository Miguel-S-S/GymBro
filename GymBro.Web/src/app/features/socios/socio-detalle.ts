import { DatePipe } from '@angular/common';
import { Component, effect, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { RouterLink } from '@angular/router';
import { SociosApi } from '../../core/api/socios.api';
import { OBJETIVO_ETIQUETA, SocioDetalle as SocioDetalleModelo } from '../../core/models/socio.models';
import { mensajeDeError } from '../../core/util/api-error';
import { SesionesPanel } from '../sesiones/sesiones-panel';
import { MedicionesPanel } from './mediciones-panel';

@Component({
  selector: 'app-socio-detalle',
  imports: [DatePipe, RouterLink, MatButtonModule, MatIconModule, MatProgressBarModule, MatTabsModule, MedicionesPanel, SesionesPanel],
  template: `
    <a mat-button routerLink="/socios" class="volver"><mat-icon>arrow_back</mat-icon> Socios</a>

    @if (cargando()) {
      <mat-progress-bar mode="indeterminate" />
    } @else if (error()) {
      <div class="estado error">
        <mat-icon>error_outline</mat-icon>
        <span>{{ error() }}</span>
      </div>
    } @else if (socio(); as s) {
      <div class="encabezado">
        <div>
          <h1>{{ s.nombreCompleto }}</h1>
          <p class="subtitulo">{{ s.email }}</p>
        </div>
      </div>

      <dl class="tarjeta perfil">
        <div><dt>Alta</dt><dd>{{ s.fechaAlta | date: 'dd/MM/yyyy' }}</dd></div>
        <div><dt>Género</dt><dd>{{ s.genero || '—' }}</dd></div>
        <div><dt>Nacimiento</dt><dd>{{ s.fechaNacimiento ? (s.fechaNacimiento | date: 'dd/MM/yyyy' : 'UTC') : '—' }}</dd></div>
        <div><dt>Objetivo</dt><dd>{{ s.objetivo ? etiquetaObjetivo[s.objetivo] : '—' }}</dd></div>
        <div><dt>Nivel</dt><dd>{{ s.nivel ?? '—' }}</dd></div>
      </dl>

      <mat-tab-group class="pestanias" mat-stretch-tabs="false" animationDuration="0ms">
        <mat-tab label="Mediciones físicas">
          <app-mediciones-panel [socioId]="s.id" />
        </mat-tab>
        <mat-tab label="Sesiones de entrenamiento">
          <app-sesiones-panel [socioId]="s.id" />
        </mat-tab>
      </mat-tab-group>
    }
  `,
  styles: `
    .volver { margin: -8px 0 8px -12px; }
    .perfil {
      display: grid; grid-template-columns: repeat(auto-fit, minmax(140px, 1fr)); gap: 12px; margin: 0 0 16px;
      dt { font: var(--mat-sys-label-medium); color: var(--mat-sys-on-surface-variant); }
      dd { margin: 2px 0 0; font: var(--mat-sys-body-large); }
    }
    .pestanias { margin-top: 8px; }
  `,
})
export class SocioDetalle {
  private readonly api = inject(SociosApi);

  /** Parámetro de ruta :id (withComponentInputBinding). */
  readonly id = input.required<string>();

  readonly etiquetaObjetivo = OBJETIVO_ETIQUETA;
  readonly socio = signal<SocioDetalleModelo | null>(null);
  readonly cargando = signal(true);
  readonly error = signal<string | null>(null);

  constructor() {
    effect(() => {
      const id = this.id();
      this.cargando.set(true);
      this.error.set(null);
      this.api.obtener(id).subscribe({
        next: (s) => {
          this.socio.set(s);
          this.cargando.set(false);
        },
        error: (e) => {
          this.error.set(mensajeDeError(e, 'No se pudo cargar el socio.'));
          this.cargando.set(false);
        },
      });
    });
  }
}
