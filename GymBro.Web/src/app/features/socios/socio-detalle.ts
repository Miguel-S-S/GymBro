import { DatePipe } from '@angular/common';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { Router, RouterLink } from '@angular/router';
import { SociosApi } from '../../core/api/socios.api';
import { AuthService } from '../../core/auth/auth.service';
import {
  ESTADO_ACCESO_ETIQUETA,
  OBJETIVO_ETIQUETA,
  SocioDetalle as SocioDetalleModelo,
} from '../../core/models/socio.models';
import { ConfirmarDialog } from '../../core/ui/confirmar-dialog';
import { mensajeDeError } from '../../core/util/api-error';
import { SesionesPanel } from '../sesiones/sesiones-panel';
import { InvitacionDialog } from './invitacion-dialog';
import { RutinasPanel } from '../rutinas/rutinas-panel';
import { MedicionesPanel } from './mediciones-panel';
import { SocioEditarDialog } from './socio-editar-dialog';

@Component({
  selector: 'app-socio-detalle',
  imports: [DatePipe, RouterLink, MatButtonModule, MatIconModule, MatProgressBarModule, MatTabsModule, MedicionesPanel, RutinasPanel, SesionesPanel],
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
        @if (esDueno()) {
          <div class="acciones">
            @if (s.estadoAcceso !== 'Activo') {
              <button mat-stroked-button [disabled]="procesando()" (click)="editar(s)"><mat-icon>edit</mat-icon> Corregir ficha</button>
              <button mat-stroked-button [disabled]="procesando()" (click)="nuevaInvitacion(s)"><mat-icon>link</mat-icon> Nueva invitación</button>
            }
            <button mat-button class="baja" [disabled]="procesando()" (click)="darDeBaja(s)">
              <mat-icon>person_remove</mat-icon> {{ s.estadoAcceso === 'Activo' ? 'Desvincular' : 'Dar de baja' }}
            </button>
          </div>
        }
      </div>

      @if (esDueno() && s.estadoAcceso !== 'Activo') {
        <p class="aviso">
          <mat-icon>hourglass_top</mat-icon>
          {{ s.estadoAcceso === 'Invitado'
            ? 'Todavía no activó su cuenta: tiene una invitación vigente. Hasta que la use podés corregir su ficha.'
            : 'No tiene acceso: la invitación venció o fue reemplazada. Generá una nueva para que pueda activar su cuenta.' }}
        </p>
      }

      <dl class="tarjeta perfil">
        @if (esDueno() && s.dni) { <div><dt>DNI</dt><dd>{{ s.dni }}</dd></div> }
        <div><dt>Acceso</dt><dd>{{ etiquetaAcceso[s.estadoAcceso] }}</dd></div>
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
        <mat-tab label="Rutinas">
          <app-rutinas-panel [socioId]="s.id" />
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
    .acciones { display: flex; flex-wrap: wrap; gap: 8px; align-items: center; }
    .baja { color: var(--mat-sys-error); }
  `,
})
export class SocioDetalle {
  private readonly api = inject(SociosApi);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);
  private readonly router = inject(Router);

  /** Parámetro de ruta :id (withComponentInputBinding). */
  readonly id = input.required<string>();

  readonly esDueno = computed(() => this.auth.rol() === 'AdministradorGimnasio');
  readonly procesando = signal(false);
  readonly etiquetaObjetivo = OBJETIVO_ETIQUETA;
  readonly etiquetaAcceso = ESTADO_ACCESO_ETIQUETA;
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

  editar(s: SocioDetalleModelo): void {
    this.dialog
      .open(SocioEditarDialog, { data: s, width: '480px', maxWidth: '95vw' })
      .afterClosed()
      .subscribe((actualizado?: SocioDetalleModelo) => {
        if (!actualizado) return;
        this.socio.set(actualizado);
        this.snack.open('Ficha actualizada', 'Cerrar', { duration: 4000 });
      });
  }

  nuevaInvitacion(s: SocioDetalleModelo): void {
    this.procesando.set(true);
    this.api.renovarInvitacion(s.id).subscribe({
      next: (invitacion) => {
        this.procesando.set(false);
        this.socio.set({ ...s, estadoAcceso: 'Invitado' });
        this.dialog.open(InvitacionDialog, {
          data: { nombre: s.nombreCompleto, invitacion },
          width: '520px',
          maxWidth: '95vw',
          disableClose: true,
        });
      },
      error: (e) => {
        this.procesando.set(false);
        this.snack.open(mensajeDeError(e, 'No se pudo emitir la invitación.'), 'Cerrar', { duration: 6000 });
      },
    });
  }

  darDeBaja(s: SocioDetalleModelo): void {
    const activo = s.estadoAcceso === 'Activo';
    this.dialog
      .open(ConfirmarDialog, {
        data: {
          titulo: activo ? `Desvincular a ${s.nombreCompleto}` : `Dar de baja a ${s.nombreCompleto}`,
          mensaje: activo
            ? 'El socio deja de pertenecer al gimnasio y se quitan sus asignaciones a entrenadores. Conserva su cuenta y su historial como socio independiente.'
            : 'Se elimina la ficha, se anula su invitación y se liberan su DNI y su email. Esta acción no se puede deshacer.',
          confirmar: activo ? 'Desvincular' : 'Dar de baja',
          peligro: true,
        },
      })
      .afterClosed()
      .subscribe((confirmado?: boolean) => {
        if (!confirmado) return;
        this.procesando.set(true);
        this.api.baja(s.id).subscribe({
          next: () => {
            this.snack.open(activo ? 'Socio desvinculado del gimnasio' : 'Socio dado de baja', 'Cerrar', { duration: 4000 });
            this.router.navigate(['/socios']);
          },
          error: (e) => {
            this.procesando.set(false);
            this.snack.open(mensajeDeError(e, 'No se pudo completar la baja.'), 'Cerrar', { duration: 7000 });
          },
        });
      });
  }
}
