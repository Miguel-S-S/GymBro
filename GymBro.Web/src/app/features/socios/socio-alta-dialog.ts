import { Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { SociosApi } from '../../core/api/socios.api';
import { SocioResumen } from '../../core/models/socio.models';
import { mensajeDeError } from '../../core/util/api-error';

@Component({
  selector: 'app-socio-alta-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule, MatProgressBarModule],
  template: `
    <h2 mat-dialog-title>Nuevo socio</h2>
    <form [formGroup]="form" (ngSubmit)="guardar()">
      <mat-dialog-content>
        <div class="campos">
          <mat-form-field>
            <mat-label>Nombre</mat-label>
            <input matInput formControlName="nombre" autocomplete="given-name" cdkFocusInitial />
            @if (form.controls.nombre.hasError('required')) { <mat-error>Ingresá el nombre.</mat-error> }
          </mat-form-field>
          <mat-form-field>
            <mat-label>Apellido</mat-label>
            <input matInput formControlName="apellido" autocomplete="family-name" />
            @if (form.controls.apellido.hasError('required')) { <mat-error>Ingresá el apellido.</mat-error> }
          </mat-form-field>
          <mat-form-field>
            <mat-label>Email</mat-label>
            <input matInput type="email" formControlName="email" autocomplete="email" />
            @if (form.controls.email.hasError('required')) { <mat-error>Ingresá el email.</mat-error> }
            @if (form.controls.email.hasError('email')) { <mat-error>El email no es válido.</mat-error> }
          </mat-form-field>
        </div>
        <p class="nota">
          La API registra por ahora nombre, apellido y email. Género, fecha de nacimiento, objetivo y nivel
          existen en el dominio pero todavía no se pueden cargar desde el alta.
        </p>
        @if (error()) { <p class="error">{{ error() }}</p> }
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
    .nota { font: var(--mat-sys-body-small); color: var(--mat-sys-on-surface-variant); }
    .error { color: var(--mat-sys-error); }
  `,
})
export class SocioAltaDialog {
  private readonly api = inject(SociosApi);
  private readonly ref = inject<MatDialogRef<SocioAltaDialog, SocioResumen>>(MatDialogRef);

  readonly form = inject(NonNullableFormBuilder).group({
    nombre: ['', [Validators.required, Validators.maxLength(100)]],
    apellido: ['', [Validators.required, Validators.maxLength(100)]],
    email: ['', [Validators.required, Validators.email]],
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
    this.api.crear({ nombre: v.nombre.trim(), apellido: v.apellido.trim(), email: v.email.trim() }).subscribe({
      next: (creado) => this.ref.close(creado),
      error: (e) => {
        this.error.set(mensajeDeError(e, 'No se pudo registrar el socio.'));
        this.guardando.set(false);
      },
    });
  }
}
