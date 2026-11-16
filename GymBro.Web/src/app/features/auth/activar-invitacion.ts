import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { InvitacionInfo } from '../../core/auth/auth.models';
import { AuthService } from '../../core/auth/auth.service';
import { mensajeDeError } from '../../core/util/api-error';
import { clavesCoinciden, validarNacimiento } from '../../core/util/validadores';

/**
 * Pantalla pública a la que llega el socio con el enlace que le dio el gimnasio (/activar/:token).
 * Crea su contraseña, completa género y fecha de nacimiento y acepta el tratamiento de datos de salud.
 */
@Component({
  selector: 'app-activar-invitacion',
  imports: [
    DatePipe, ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatButtonModule, MatIconModule,
    MatCheckboxModule, MatSelectModule, MatProgressBarModule,
  ],
  template: `
    <div class="pagina">
      <div class="tarjeta">
        <div class="marca"><mat-icon>fitness_center</mat-icon><span>GymBro</span></div>

        @if (cargando()) {
          <mat-progress-bar mode="indeterminate" />
        } @else if (errorInvitacion()) {
          <h1>Invitación no disponible</h1>
          <p class="error" role="alert">{{ errorInvitacion() }}</p>
        } @else if (info(); as i) {
          <h1>Activá tu cuenta</h1>
          <p>
            <strong>{{ i.gimnasio }}</strong> te registró como socio. Elegí tu contraseña y completá tus datos para empezar.
          </p>

          <form [formGroup]="form" (ngSubmit)="activar()">
            <mat-form-field>
              <mat-label>Nombre</mat-label>
              <input matInput [value]="i.nombre + ' ' + i.apellido" readonly />
            </mat-form-field>
            <mat-form-field>
              <mat-label>Email (será tu usuario)</mat-label>
              <input matInput [value]="i.email" readonly />
            </mat-form-field>

            <mat-form-field>
              <mat-label>Contraseña</mat-label>
              <input matInput [type]="verClave() ? 'text' : 'password'" formControlName="password" autocomplete="new-password" cdkFocusInitial />
              <button mat-icon-button matSuffix type="button" (click)="verClave.set(!verClave())"
                      [attr.aria-label]="verClave() ? 'Ocultar contraseña' : 'Mostrar contraseña'">
                <mat-icon>{{ verClave() ? 'visibility_off' : 'visibility' }}</mat-icon>
              </button>
              @if (form.controls.password.hasError('required')) { <mat-error>Elegí una contraseña.</mat-error> }
              @else if (form.controls.password.hasError('minlength')) { <mat-error>Debe tener al menos 8 caracteres.</mat-error> }
            </mat-form-field>
            <mat-form-field>
              <mat-label>Repetir contraseña</mat-label>
              <input matInput [type]="verClave() ? 'text' : 'password'" formControlName="repetir" autocomplete="new-password" />
              @if (form.controls.repetir.hasError('required')) { <mat-error>Repetí la contraseña.</mat-error> }
            </mat-form-field>
            @if (form.hasError('noCoinciden') && form.controls.repetir.touched) {
              <p class="error" role="alert">Las contraseñas no coinciden.</p>
            }

            <mat-form-field>
              <mat-label>Fecha de nacimiento</mat-label>
              <input matInput type="date" formControlName="fechaNacimiento" />
              @if (form.controls.fechaNacimiento.hasError('required')) { <mat-error>Ingresá tu fecha de nacimiento.</mat-error> }
              @else if (form.controls.fechaNacimiento.hasError('nacimientoInvalido')) { <mat-error>Fecha inválida (edad mínima: 14 años).</mat-error> }
            </mat-form-field>
            <mat-form-field>
              <mat-label>Género</mat-label>
              <mat-select formControlName="genero">
                <mat-option value="F">Femenino</mat-option>
                <mat-option value="M">Masculino</mat-option>
                <mat-option value="X">Otro / prefiero no decirlo</mat-option>
              </mat-select>
              @if (form.controls.genero.hasError('required')) { <mat-error>Elegí una opción.</mat-error> }
            </mat-form-field>

            <mat-checkbox formControlName="consentimiento">
              Acepto que GymBro trate mis datos de salud para armar y seguir mis rutinas (Ley 25.326).
            </mat-checkbox>
            @if (form.controls.consentimiento.touched && form.controls.consentimiento.hasError('required')) {
              <p class="error" role="alert">Necesitamos tu consentimiento para continuar.</p>
            }

            @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
            @if (enviando()) { <mat-progress-bar mode="indeterminate" /> }
            <button mat-flat-button type="submit" [disabled]="enviando()">Activar cuenta</button>
            <p class="vence">La invitación vence el {{ i.expiraUtc | date: 'dd/MM/yyyy HH:mm' }}.</p>
          </form>
        }
      </div>
    </div>
  `,
  styles: `
    .pagina { min-height: 100dvh; display: grid; place-items: center; padding: 16px; background: var(--mat-sys-surface-container-low); }
    .tarjeta { width: 100%; max-width: 420px; display: flex; flex-direction: column; gap: 8px; padding: 28px 24px;
      background: var(--mat-sys-surface); border-radius: 16px; border: 1px solid var(--mat-sys-outline-variant); box-sizing: border-box; }
    form { display: flex; flex-direction: column; gap: 8px; }
    .marca { display: flex; align-items: center; gap: 8px; font: var(--mat-sys-title-large); color: var(--mat-sys-primary); }
    h1 { font: var(--mat-sys-headline-small); margin: 8px 0 4px; }
    p { margin: 0; }
    .error { color: var(--mat-sys-error); }
    .vence { font: var(--mat-sys-body-small); color: var(--mat-sys-on-surface-variant); text-align: center; }
  `,
})
export class ActivarInvitacion implements OnInit {
  private readonly auth = inject(AuthService);

  /** Parámetro de ruta :token (withComponentInputBinding). */
  readonly token = input.required<string>();

  readonly info = signal<InvitacionInfo | null>(null);
  readonly cargando = signal(true);
  readonly errorInvitacion = signal<string | null>(null);
  readonly enviando = signal(false);
  readonly error = signal<string | null>(null);
  readonly verClave = signal(false);

  readonly form = inject(NonNullableFormBuilder).group(
    {
      password: ['', [Validators.required, Validators.minLength(8)]],
      repetir: ['', [Validators.required]],
      fechaNacimiento: ['', [Validators.required, validarNacimiento]],
      genero: ['', [Validators.required]],
      consentimiento: [false, [Validators.requiredTrue]],
    },
    { validators: clavesCoinciden },
  );

  ngOnInit(): void {
    // El token viene de la ruta (input enlazado): en ngOnInit ya está disponible.
    this.consultar();
  }

  private consultar(): void {
    this.auth.consultarInvitacion(this.token()).subscribe({
      next: (i) => {
        this.info.set(i);
        this.cargando.set(false);
      },
      error: (e) => {
        this.errorInvitacion.set(mensajeDeError(e, 'No se pudo validar la invitación.'));
        this.cargando.set(false);
      },
    });
  }

  activar(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.enviando.set(true);
    this.error.set(null);
    this.auth
      .activarInvitacion({
        token: this.token(),
        password: v.password,
        fechaNacimiento: v.fechaNacimiento,
        genero: v.genero as 'M' | 'F' | 'X',
        consentimientoDatosSalud: v.consentimiento,
      })
      .subscribe({
        next: () => this.auth.irAInicio(),
        error: (e) => {
          this.error.set(mensajeDeError(e, 'No se pudo activar la cuenta.'));
          this.enviando.set(false);
        },
      });
  }
}
