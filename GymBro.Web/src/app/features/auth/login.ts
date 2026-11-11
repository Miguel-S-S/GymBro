import { Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { AuthService } from '../../core/auth/auth.service';
import { mensajeDeError } from '../../core/util/api-error';

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatButtonModule, MatIconModule, MatProgressBarModule],
  template: `
    <div class="pagina">
      <form class="tarjeta" [formGroup]="form" (ngSubmit)="ingresar()">
        <div class="marca"><mat-icon>fitness_center</mat-icon><span>GymBro</span></div>
        <h1>Iniciar sesión</h1>

        <mat-form-field>
          <mat-label>Email</mat-label>
          <input matInput type="email" formControlName="email" autocomplete="username" cdkFocusInitial />
          @if (form.controls.email.hasError('required')) { <mat-error>Ingresá tu email.</mat-error> }
          @if (form.controls.email.hasError('email')) { <mat-error>El email no es válido.</mat-error> }
        </mat-form-field>

        <mat-form-field>
          <mat-label>Contraseña</mat-label>
          <input matInput [type]="verClave() ? 'text' : 'password'" formControlName="password" autocomplete="current-password" />
          <button mat-icon-button matSuffix type="button" (click)="verClave.set(!verClave())"
                  [attr.aria-label]="verClave() ? 'Ocultar contraseña' : 'Mostrar contraseña'">
            <mat-icon>{{ verClave() ? 'visibility_off' : 'visibility' }}</mat-icon>
          </button>
          @if (form.controls.password.hasError('required')) { <mat-error>Ingresá tu contraseña.</mat-error> }
        </mat-form-field>

        @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
        @if (ingresando()) { <mat-progress-bar mode="indeterminate" /> }
        <button mat-flat-button type="submit" [disabled]="ingresando()">Ingresar</button>
      </form>
    </div>
  `,
  styles: `
    .pagina { min-height: 100dvh; display: grid; place-items: center; padding: 16px; background: var(--mat-sys-surface-container-low); }
    .tarjeta { width: 100%; max-width: 380px; display: flex; flex-direction: column; gap: 8px; padding: 28px 24px;
      background: var(--mat-sys-surface); border-radius: 16px; border: 1px solid var(--mat-sys-outline-variant); box-sizing: border-box; }
    .marca { display: flex; align-items: center; gap: 8px; font: var(--mat-sys-title-large); color: var(--mat-sys-primary); }
    h1 { font: var(--mat-sys-headline-small); margin: 8px 0 12px; }
    .error { color: var(--mat-sys-error); margin: 0; }
  `,
})
export class Login {
  private readonly auth = inject(AuthService);

  readonly form = inject(NonNullableFormBuilder).group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]],
  });
  readonly ingresando = signal(false);
  readonly error = signal<string | null>(null);
  readonly verClave = signal(false);

  ingresar(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.ingresando.set(true);
    this.error.set(null);
    const v = this.form.getRawValue();
    this.auth.login({ email: v.email.trim(), password: v.password }).subscribe({
      next: () => this.auth.irAInicio(),
      error: (e) => {
        this.error.set(mensajeDeError(e, 'No se pudo iniciar sesión.'));
        this.ingresando.set(false);
      },
    });
  }
}