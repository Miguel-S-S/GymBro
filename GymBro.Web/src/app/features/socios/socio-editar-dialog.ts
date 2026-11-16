import { Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { SociosApi } from '../../core/api/socios.api';
import { SocioDetalle } from '../../core/models/socio.models';
import { mensajeDeError } from '../../core/util/api-error';

/** Corrige la ficha de un socio que todavía no activó su cuenta (después, sus datos los gestiona él). */
@Component({
  selector: 'app-socio-editar-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule, MatProgressBarModule],
  template: `
    <h2 mat-dialog-title>Corregir ficha</h2>
    <form [formGroup]="form" (ngSubmit)="guardar()">
      <mat-dialog-content>
        <div class="campos">
          <mat-form-field>
            <mat-label>Nombre</mat-label>
            <input matInput formControlName="nombre" autocomplete="off" cdkFocusInitial />
            @if (form.controls.nombre.hasError('required')) { <mat-error>Ingresá el nombre.</mat-error> }
          </mat-form-field>
          <mat-form-field>
            <mat-label>Apellido</mat-label>
            <input matInput formControlName="apellido" autocomplete="off" />
            @if (form.controls.apellido.hasError('required')) { <mat-error>Ingresá el apellido.</mat-error> }
          </mat-form-field>
          <mat-form-field>
            <mat-label>DNI</mat-label>
            <input matInput formControlName="dni" inputmode="numeric" maxlength="9" autocomplete="off" />
            @if (form.controls.dni.hasError('required')) { <mat-error>Ingresá el DNI.</mat-error> }
            @else if (form.controls.dni.hasError('pattern')) { <mat-error>El DNI debe tener entre 7 y 9 dígitos.</mat-error> }
          </mat-form-field>
          <mat-form-field>
            <mat-label>Email</mat-label>
            <input matInput type="email" formControlName="email" autocomplete="off" />
            @if (form.controls.email.hasError('required')) { <mat-error>Ingresá el email.</mat-error> }
            @else if (form.controls.email.hasError('email')) { <mat-error>El email no es válido.</mat-error> }
          </mat-form-field>
        </div>
        @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
      </mat-dialog-content>
      @if (guardando()) { <mat-progress-bar mode="indeterminate" /> }
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close [disabled]="guardando()">Cancelar</button>
        <button mat-flat-button type="submit" [disabled]="guardando()">Guardar</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `
    .campos { display: flex; flex-direction: column; gap: 12px; padding-top: 8px; }
    .error { color: var(--mat-sys-error); }
  `,
})
export class SocioEditarDialog {
  private readonly api = inject(SociosApi);
  private readonly socio = inject<SocioDetalle>(MAT_DIALOG_DATA);
  private readonly ref = inject<MatDialogRef<SocioEditarDialog, SocioDetalle>>(MatDialogRef);

  readonly form = inject(NonNullableFormBuilder).group({
    nombre: [this.socio.nombre, [Validators.required, Validators.maxLength(100)]],
    apellido: [this.socio.apellido, [Validators.required, Validators.maxLength(100)]],
    dni: [this.socio.dni ?? '', [Validators.required, Validators.pattern(/^\d{7,9}$/)]],
    email: [this.socio.email, [Validators.required, Validators.email]],
  });
  readonly guardando = signal(false);
  readonly error = signal<string | null>(null);

  guardar(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.guardando.set(true);
    this.error.set(null);
    this.api
      .actualizar(this.socio.id, { nombre: v.nombre.trim(), apellido: v.apellido.trim(), email: v.email.trim(), dni: v.dni.trim() })
      .subscribe({
        next: (actualizado) => this.ref.close(actualizado),
        error: (e) => {
          this.error.set(mensajeDeError(e, 'No se pudo guardar la ficha.'));
          this.guardando.set(false);
        },
      });
  }
}
