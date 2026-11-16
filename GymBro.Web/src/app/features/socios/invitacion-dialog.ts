import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { Invitacion } from '../../core/models/socio.models';
import { InvitacionEnlace } from './invitacion-enlace';

export interface InvitacionDialogDatos {
  nombre: string;
  invitacion: Invitacion;
}

/** Muestra una invitación recién emitida (la anterior ya no vale). */
@Component({
  selector: 'app-invitacion-dialog',
  imports: [MatDialogModule, MatButtonModule, InvitacionEnlace],
  template: `
    <h2 mat-dialog-title>Nueva invitación</h2>
    <mat-dialog-content><app-invitacion-enlace [invitacion]="datos.invitacion" [nombre]="datos.nombre" /></mat-dialog-content>
    <mat-dialog-actions align="end"><button mat-flat-button mat-dialog-close>Listo</button></mat-dialog-actions>
  `,
})
export class InvitacionDialog {
  readonly datos = inject<InvitacionDialogDatos>(MAT_DIALOG_DATA);
}
