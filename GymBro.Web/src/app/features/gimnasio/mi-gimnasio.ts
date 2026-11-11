import { Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AccesoApi } from '../../core/api/acceso.api';
import { SociosApi } from '../../core/api/socios.api';
import { Entrenador, Gimnasio } from '../../core/models/acceso.models';
import { SocioResumen } from '../../core/models/socio.models';
import { mensajeDeError } from '../../core/util/api-error';

/** Dueño de Gimnasio: código de vinculación, política de revisión de rutinas y Personal Trainers. */
@Component({
  selector: 'app-mi-gimnasio',
  imports: [ReactiveFormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule,
    MatProgressBarModule, MatSelectModule, MatSlideToggleModule],
  template: `
    @if (gimnasio(); as g) {
      <div class="encabezado"><div><h1>{{ g.nombre }}</h1>
        <p class="subtitulo">Código para que los socios se vinculen: <code>{{ g.codigo }}</code></p></div></div>

      <section>
        <h2>Revisión de rutinas</h2>
        <mat-slide-toggle [checked]="g.requiereAprobacionRutinas" (change)="cambiarPolitica($event.checked)">
          Las rutinas generadas por la IA requieren revisión humana antes de llegar al socio
        </mat-slide-toggle>
      </section>

      <section>
        <h2>Personal Trainers</h2>
        @for (e of entrenadores(); track e.id) {
          <div class="fila"><mat-icon>sports</mat-icon><span>{{ e.nombreCompleto }} · {{ e.email }}</span></div>
        } @empty {
          <p class="ayuda">Todavía no cargaste entrenadores.</p>
        }

        <form class="formulario" [formGroup]="formEntrenador" (ngSubmit)="crearEntrenador()">
          <mat-form-field><mat-label>Nombre completo</mat-label><input matInput formControlName="nombreCompleto" /></mat-form-field>
          <mat-form-field><mat-label>Email</mat-label><input matInput type="email" formControlName="email" autocomplete="off" /></mat-form-field>
          <mat-form-field><mat-label>Contraseña inicial (mín. 8)</mat-label>
            <input matInput type="password" formControlName="password" autocomplete="new-password" /></mat-form-field>
          <div><button mat-flat-button type="submit" [disabled]="guardando()">Agregar entrenador</button></div>
        </form>
      </section>

      <section>
        <h2>Asignar socio a un entrenador</h2>
        <form class="formulario" [formGroup]="formAsignacion" (ngSubmit)="asignar()">
          <mat-form-field><mat-label>Entrenador</mat-label>
            <mat-select formControlName="entrenadorId">
              @for (e of entrenadores(); track e.id) { <mat-option [value]="e.id">{{ e.nombreCompleto }}</mat-option> }
            </mat-select></mat-form-field>
          <mat-form-field><mat-label>Socio</mat-label>
            <mat-select formControlName="socioId">
              @for (s of socios(); track s.id) { <mat-option [value]="s.id">{{ s.nombreCompleto }}</mat-option> }
            </mat-select></mat-form-field>
          <div><button mat-flat-button type="submit" [disabled]="formAsignacion.invalid || guardando()">Asignar</button></div>
        </form>
      </section>
    } @else if (error()) {
      <div class="estado error"><mat-icon>cloud_off</mat-icon><span>{{ error() }}</span></div>
    } @else {
      <mat-progress-bar mode="indeterminate" />
    }
  `,
  styles: `
    section { margin-bottom: 32px; }
    h2 { font: var(--mat-sys-title-medium); }
    .formulario { display: grid; gap: 12px; max-width: 420px; margin-top: 16px; }
    .fila { display: flex; align-items: center; gap: 8px; padding: 4px 0; }
    .ayuda { color: var(--mat-sys-on-surface-variant); }
  `,
})
export class MiGimnasio {
  private readonly api = inject(AccesoApi);
  private readonly sociosApi = inject(SociosApi);
  private readonly snack = inject(MatSnackBar);
  private readonly fb = inject(NonNullableFormBuilder);

  readonly gimnasio = signal<Gimnasio | null>(null);
  readonly entrenadores = signal<Entrenador[]>([]);
  readonly socios = signal<SocioResumen[]>([]);
  readonly guardando = signal(false);
  readonly error = signal<string | null>(null);

  readonly formEntrenador = this.fb.group({
    nombreCompleto: ['', [Validators.required, Validators.maxLength(200)]],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(8)]],
  });
  readonly formAsignacion = this.fb.group({
    entrenadorId: ['', Validators.required],
    socioId: ['', Validators.required],
  });

  constructor() {
    this.api.miGimnasio().subscribe({
      next: (g) => this.gimnasio.set(g),
      error: (e) => this.error.set(mensajeDeError(e, 'No se pudo cargar el gimnasio.')),
    });
    this.api.listarEntrenadores().subscribe((l) => this.entrenadores.set(l));
    this.sociosApi.listar().subscribe((l) => this.socios.set(l));
  }

  cambiarPolitica(requiere: boolean): void {
    this.api.actualizarPoliticaRevision(requiere).subscribe({
      next: (g) => {
        this.gimnasio.set(g);
        this.snack.open('Política de revisión actualizada', 'Cerrar', { duration: 3000 });
      },
      error: (e) => this.snack.open(mensajeDeError(e), 'Cerrar', { duration: 5000 }),
    });
  }

  crearEntrenador(): void {
    if (this.formEntrenador.invalid) { this.formEntrenador.markAllAsTouched(); return; }
    this.guardando.set(true);
    this.api.crearEntrenador(this.formEntrenador.getRawValue()).subscribe({
      next: (e) => {
        this.entrenadores.update((l) => [...l, e]);
        this.formEntrenador.reset();
        this.guardando.set(false);
        this.snack.open(`Entrenador ${e.nombreCompleto} agregado`, 'Cerrar', { duration: 4000 });
      },
      error: (e) => { this.snack.open(mensajeDeError(e), 'Cerrar', { duration: 6000 }); this.guardando.set(false); },
    });
  }

  asignar(): void {
    const v = this.formAsignacion.getRawValue();
    this.guardando.set(true);
    this.api.asignarSocio(v.entrenadorId, v.socioId).subscribe({
      next: () => {
        this.formAsignacion.reset();
        this.guardando.set(false);
        this.snack.open('Socio asignado', 'Cerrar', { duration: 3000 });
      },
      error: (e) => { this.snack.open(mensajeDeError(e), 'Cerrar', { duration: 6000 }); this.guardando.set(false); },
    });
  }
}
