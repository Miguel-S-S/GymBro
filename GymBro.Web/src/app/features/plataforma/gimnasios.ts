import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { AccesoApi } from '../../core/api/acceso.api';
import { Gimnasio } from '../../core/models/acceso.models';
import { mensajeDeError } from '../../core/util/api-error';

/** Administrador de la Plataforma: alta de instituciones y de su Dueño de Gimnasio. */
@Component({
  selector: 'app-gimnasios',
  imports: [DatePipe, ReactiveFormsModule, MatTableModule, MatButtonModule, MatIconModule, MatFormFieldModule,
    MatInputModule, MatSlideToggleModule, MatProgressBarModule],
  template: `
    <div class="encabezado">
      <div>
        <h1>Gimnasios</h1>
        <p class="subtitulo">{{ gimnasios().length }} instituciones</p>
      </div>
      <button mat-flat-button (click)="formVisible.set(!formVisible())">
        <mat-icon>{{ formVisible() ? 'close' : 'add_business' }}</mat-icon>
        {{ formVisible() ? 'Cancelar' : 'Nuevo gimnasio' }}
      </button>
    </div>

    @if (formVisible()) {
      <form class="formulario" [formGroup]="form" (ngSubmit)="crear()">
        <mat-form-field><mat-label>Nombre del gimnasio</mat-label><input matInput formControlName="nombre" /></mat-form-field>
        <mat-form-field><mat-label>Nombre del dueño</mat-label><input matInput formControlName="duenoNombreCompleto" /></mat-form-field>
        <mat-form-field><mat-label>Email del dueño</mat-label><input matInput type="email" formControlName="duenoEmail" autocomplete="off" /></mat-form-field>
        <mat-form-field>
          <mat-label>Contraseña inicial (mín. 8)</mat-label>
          <input matInput type="password" formControlName="duenoPassword" autocomplete="new-password" />
        </mat-form-field>
        <mat-slide-toggle formControlName="requiereAprobacionRutinas">Revisión humana obligatoria de rutinas</mat-slide-toggle>
        @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
        <div><button mat-flat-button type="submit" [disabled]="guardando()">Crear gimnasio</button></div>
      </form>
    }

    @if (cargando()) {
      <mat-progress-bar mode="indeterminate" />
    } @else if (gimnasios().length === 0) {
      <div class="estado"><mat-icon>store</mat-icon><span>Todavía no hay gimnasios dados de alta.</span></div>
    } @else {
      <div class="tabla-contenedor">
        <table mat-table [dataSource]="gimnasios()">
          <ng-container matColumnDef="nombre"><th mat-header-cell *matHeaderCellDef>Nombre</th><td mat-cell *matCellDef="let g">{{ g.nombre }}</td></ng-container>
          <ng-container matColumnDef="codigo"><th mat-header-cell *matHeaderCellDef>Código</th><td mat-cell *matCellDef="let g"><code>{{ g.codigo }}</code></td></ng-container>
          <ng-container matColumnDef="revision"><th mat-header-cell *matHeaderCellDef>Revisión de rutinas</th><td mat-cell *matCellDef="let g">{{ g.requiereAprobacionRutinas ? 'Obligatoria' : 'Opcional' }}</td></ng-container>
          <ng-container matColumnDef="alta"><th mat-header-cell *matHeaderCellDef>Alta</th><td mat-cell *matCellDef="let g">{{ g.fechaAlta | date: 'dd/MM/yyyy' }}</td></ng-container>
          <tr mat-header-row *matHeaderRowDef="columnas"></tr>
          <tr mat-row *matRowDef="let g; columns: columnas"></tr>
        </table>
      </div>
    }
  `,
  styles: `
    .formulario { display: grid; gap: 12px; max-width: 480px; margin-bottom: 24px; }
    .error { color: var(--mat-sys-error); margin: 0; }
  `,
})
export class Gimnasios {
  private readonly api = inject(AccesoApi);
  private readonly snack = inject(MatSnackBar);

  readonly columnas = ['nombre', 'codigo', 'revision', 'alta'];
  readonly gimnasios = signal<Gimnasio[]>([]);
  readonly cargando = signal(true);
  readonly guardando = signal(false);
  readonly formVisible = signal(false);
  readonly error = signal<string | null>(null);

  readonly form = inject(NonNullableFormBuilder).group({
    nombre: ['', [Validators.required, Validators.maxLength(150)]],
    duenoNombreCompleto: ['', [Validators.required, Validators.maxLength(200)]],
    duenoEmail: ['', [Validators.required, Validators.email]],
    duenoPassword: ['', [Validators.required, Validators.minLength(8)]],
    requiereAprobacionRutinas: [true],
  });

  constructor() {
    this.cargar();
  }

  cargar(): void {
    this.api.listarGimnasios().subscribe({
      next: (g) => { this.gimnasios.set(g); this.cargando.set(false); },
      error: (e) => { this.snack.open(mensajeDeError(e), 'Cerrar', { duration: 5000 }); this.cargando.set(false); },
    });
  }

  crear(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.guardando.set(true);
    this.error.set(null);
    this.api.crearGimnasio(this.form.getRawValue()).subscribe({
      next: (g) => {
        this.gimnasios.update((l) => [...l, g]);
        this.snack.open(`Gimnasio creado. Código: ${g.codigo}`, 'Cerrar', { duration: 8000 });
        this.form.reset({ requiereAprobacionRutinas: true });
        this.formVisible.set(false);
        this.guardando.set(false);
      },
      error: (e) => { this.error.set(mensajeDeError(e, 'No se pudo crear el gimnasio.')); this.guardando.set(false); },
    });
  }
}