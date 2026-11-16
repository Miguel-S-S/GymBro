import { DatePipe } from '@angular/common';
import { Component, computed, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { Invitacion, enlaceInvitacion } from '../../core/models/socio.models';

/**
 * Muestra el enlace de activación para entregárselo al socio. El código se ve una sola vez:
 * la API solo guarda su hash, así que si se pierde hay que emitir una invitación nueva.
 */
@Component({
  selector: 'app-invitacion-enlace',
  imports: [DatePipe, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule],
  template: `
    <p class="ayuda">
      Enviale este enlace a <strong>{{ nombre() }}</strong> (por WhatsApp o mail). Con él crea su contraseña y
      acepta el tratamiento de sus datos de salud. Vale hasta el {{ invitacion().expiraUtc | date: 'dd/MM/yyyy HH:mm' }} y se puede usar una sola vez.
    </p>
    <mat-form-field>
      <mat-label>Enlace de activación</mat-label>
      <input matInput readonly [value]="enlace()" (focus)="$any($event.target).select()" />
      <button mat-icon-button matSuffix type="button" (click)="copiar()" aria-label="Copiar enlace">
        <mat-icon>{{ copiado() ? 'check' : 'content_copy' }}</mat-icon>
      </button>
    </mat-form-field>
    <p class="aviso-unico">
      <mat-icon>info</mat-icon>
      Este enlace no se vuelve a mostrar. Si lo perdés, generá una invitación nueva desde la ficha del socio.
    </p>
    @if (puedeCompartir) {
      <button mat-stroked-button type="button" (click)="compartir()"><mat-icon>share</mat-icon> Compartir</button>
    }
  `,
  styles: `
    :host { display: flex; flex-direction: column; gap: 8px; }
    .ayuda { margin: 0; }
    .aviso-unico { display: flex; gap: 8px; align-items: center; margin: 0; font: var(--mat-sys-body-small); color: var(--mat-sys-on-surface-variant); }
    mat-form-field { width: 100%; }
  `,
})
export class InvitacionEnlace {
  readonly invitacion = input.required<Invitacion>();
  readonly nombre = input.required<string>();

  readonly enlace = computed(() => enlaceInvitacion(this.invitacion().token, location.origin));
  readonly copiado = signal(false);
  readonly puedeCompartir = typeof navigator !== 'undefined' && typeof navigator.share === 'function';

  async copiar(): Promise<void> {
    try {
      await navigator.clipboard.writeText(this.enlace());
      this.copiado.set(true);
      setTimeout(() => this.copiado.set(false), 2500);
    } catch {
      /* sin permiso de portapapeles: el campo es seleccionable y se puede copiar a mano */
    }
  }

  async compartir(): Promise<void> {
    try {
      await navigator.share({ title: 'Activá tu cuenta en GymBro', url: this.enlace() });
    } catch {
      /* el usuario canceló */
    }
  }
}
