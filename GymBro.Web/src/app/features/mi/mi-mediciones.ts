import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MedicionesApi } from '../../core/api/mediciones.api';
import { AuthService } from '../../core/auth/auth.service';
import { Medicion } from '../../core/models/medicion.models';
import { mensajeDeError } from '../../core/util/api-error';

/** Mis mediciones: el Socio registra peso y altura (insumo de la rutina baseline) y ve su historial. */
@Component({
  selector: 'app-mi-mediciones',
  imports: [DatePipe, DecimalPipe, ReactiveFormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, MatProgressBarModule],
  template: `
    <h1 class="titulo">Mis mediciones</h1>

    <form class="formulario" [formGroup]="form" (ngSubmit)="registrar()">
      <mat-form-field>
        <mat-label>Peso (kg)</mat-label>
        <input matInput type="number" inputmode="decimal" formControlName="pesoKg" min="1" max="400" step="0.1" />
        <mat-error>Entre 1 y 400 kg.</mat-error>
      </mat-form-field>
      <mat-form-field>
        <mat-label>Altura (cm)</mat-label>
        <input matInput type="number" inputmode="numeric" formControlName="alturaCm" min="50" max="250" step="1" />
        <mat-error>Entre 50 y 250 cm.</mat-error>
      </mat-form-field>
      <button mat-flat-button class="principal" type="submit" [disabled]="enviando()">
        <mat-icon>save</mat-icon> Registrar medición
      </button>
    </form>

    <h2 class="subtitulo-mi">Historial</h2>
    @if (cargando()) {
      <mat-progress-bar mode="indeterminate" />
    } @else {
      @for (m of mediciones(); track m.id) {
        <div class="item">
          <strong>{{ m.fechaAlta | date: 'dd/MM/yyyy' }}</strong>
          <span>{{ m.pesoKg | number: '1.0-1' }} kg · {{ m.alturaCm | number: '1.0-0' }} cm
            @if (m.imc > 0) { · IMC {{ m.imc | number: '1.1-1' }} }</span>
        </div>
      } @empty {
        <p class="meta">Todavía no hay mediciones.</p>
      }
    }
  `,
  styles: `
    .titulo { font: var(--mat-sys-headline-small); margin: 4px 0 12px; }
    .subtitulo-mi { font: var(--mat-sys-title-medium); margin: 16px 0 8px; }
    .formulario { display: grid; grid-template-columns: 1fr 1fr; gap: 8px 12px; }
    .principal { grid-column: 1 / -1; height: 48px; }
    .item { display: flex; justify-content: space-between; gap: 8px; padding: 10px 12px; border: 1px solid var(--mat-sys-outline-variant);
      border-radius: 12px; margin-bottom: 8px; font: var(--mat-sys-body-medium); }
    .meta { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-medium); }
  `,
})
export class MiMediciones {
  private readonly api = inject(MedicionesApi);
  private readonly snack = inject(MatSnackBar);
  private readonly socioId = inject(AuthService).sesion()?.socioId ?? null;

  readonly mediciones = signal<Medicion[]>([]);
  readonly cargando = signal(true);
  readonly enviando = signal(false);

  // Mismos límites que el panel del personal (mediciones-panel.ts).
  readonly form = inject(NonNullableFormBuilder).group({
    pesoKg: [null as unknown as number, [Validators.required, Validators.min(1), Validators.max(400)]],
    alturaCm: [null as unknown as number, [Validators.required, Validators.min(50), Validators.max(250)]],
  });

  constructor() {
    this.cargar();
  }

  private cargar(): void {
    if (!this.socioId) { this.cargando.set(false); return; }
    this.api.listarPorSocio(this.socioId).subscribe({
      next: (m) => { this.mediciones.set(m); this.cargando.set(false); },
      error: (e) => { this.snack.open(mensajeDeError(e), 'Cerrar', { duration: 5000 }); this.cargando.set(false); },
    });
  }

  registrar(): void {
    if (this.form.invalid || !this.socioId) {
      this.form.markAllAsTouched();
      return;
    }
    const { pesoKg, alturaCm } = this.form.getRawValue();
    this.enviando.set(true);
    this.api.registrar({ socioId: this.socioId, pesoKg, alturaCm }).subscribe({
      next: () => {
        this.enviando.set(false);
        this.form.reset();
        this.snack.open('Medición registrada.', 'Cerrar', { duration: 3000 });
        this.cargar();
      },
      error: (e) => { this.snack.open(mensajeDeError(e), 'Cerrar', { duration: 6000 }); this.enviando.set(false); },
    });
  }
}
