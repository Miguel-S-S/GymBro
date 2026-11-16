import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';

export interface ConfirmarDatos {
  titulo: string;
  mensaje: string;
  confirmar: string;
  /** Resalta la acción como destructiva. */
  peligro?: boolean;
}

/** Diálogo de confirmación genérico: devuelve `true` si el usuario confirma. */
@Component({
  selector: 'app-confirmar-dialog',
  imports: [MatDialogModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>{{ datos.titulo }}</h2>
    <mat-dialog-content><p class="mensaje">{{ datos.mensaje }}</p></mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button [mat-dialog-close]="false">Cancelar</button>
      <button mat-flat-button [class.peligro]="datos.peligro" [mat-dialog-close]="true" cdkFocusInitial>
        {{ datos.confirmar }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .mensaje { margin: 0; white-space: pre-line; }
    .peligro { --mat-button-filled-container-color: var(--mat-sys-error); --mat-button-filled-label-text-color: var(--mat-sys-on-error); }
  `,
})
export class ConfirmarDialog {
  readonly datos = inject<ConfirmarDatos>(MAT_DIALOG_DATA);
}
