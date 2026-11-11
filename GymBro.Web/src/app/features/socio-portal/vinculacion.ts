import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AccesoApi } from '../../core/api/acceso.api';
import { SolicitudVinculacion } from '../../core/models/acceso.models';
import { mensajeDeError } from '../../core/util/api-error';

/** Socio: pide unirse a un gimnasio con su código o sigue usando la app de forma independiente. */
@Component({
  selector: 'app-vinculacion',
  imports: [DatePipe, ReactiveFormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule],
  template: `
    <div class="encabezado"><div><h1>Mi gimnasio</h1>
      <p class="subtitulo">Sin vincularte podés usar la app de forma independiente, sin revisión de un profesional.</p></div></div>

    <form class="formulario" [formGroup]="form" (ngSubmit)="solicitar()">
      <mat-form-field>
        <mat-label>Código del gimnasio</mat-label>
        <input matInput formControlName="codigo" maxlength="12" autocapitalize="characters" />
      </mat-form-field>
      <div><button mat-flat-button type="submit" [disabled]="enviando()">Solicitar vinculación</button></div>
    </form>

    <h2>Mis solicitudes</h2>
    @for (s of solicitudes(); track s.id) {
      <div class="fila">
        <mat-icon>{{ s.estado === 'Aprobada' ? 'check_circle' : s.estado === 'Rechazada' ? 'cancel' : 'hourglass_top' }}</mat-icon>
        <span>{{ s.fechaAlta | date: 'dd/MM/yyyy' }} · {{ s.estado }}{{ s.motivo ? ' — ' + s.motivo : '' }}</span>
      </div>
    } @empty {
      <p class="ayuda">Todavía no enviaste solicitudes.</p>
    }
  `,
  styles: `
    .formulario { display: grid; gap: 12px; max-width: 360px; margin-bottom: 24px; }
    .fila { display: flex; align-items: center; gap: 8px; padding: 4px 0; }
    .ayuda { color: var(--mat-sys-on-surface-variant); }
  `,
})
export class Vinculacion {
  private readonly api = inject(AccesoApi);
  private readonly snack = inject(MatSnackBar);

  readonly form = inject(NonNullableFormBuilder).group({ codigo: ['', [Validators.required, Validators.maxLength(12)]] });
  readonly solicitudes = signal<SolicitudVinculacion[]>([]);
  readonly enviando = signal(false);

  constructor() {
    this.cargar();
  }

  cargar(): void {
    this.api.misSolicitudes().subscribe((s) => this.solicitudes.set(s));
  }

  solicitar(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.enviando.set(true);
    this.api.solicitarVinculacion(this.form.getRawValue().codigo.trim()).subscribe({
      next: () => {
        this.form.reset();
        this.enviando.set(false);
        this.snack.open('Solicitud enviada. El gimnasio la va a revisar.', 'Cerrar', { duration: 5000 });
        this.cargar();
      },
      error: (e) => { this.snack.open(mensajeDeError(e), 'Cerrar', { duration: 6000 }); this.enviando.set(false); },
    });
  }
}