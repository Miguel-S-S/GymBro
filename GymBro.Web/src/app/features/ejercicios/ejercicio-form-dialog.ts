import { Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { forkJoin } from 'rxjs';
import { EjerciciosApi } from '../../core/api/ejercicios.api';
import { EquipamientoApi } from '../../core/api/equipamiento.api';
import {
  EjercicioDetalle,
  EjercicioGimnasioRequest,
  EquipamientoItem,
  LIMITES_EJERCICIO,
  TipoMedida,
  tiposElegibles,
} from '../../core/models/ejercicio.models';
import { mensajeDeError } from '../../core/util/api-error';

/** Datos del diálogo: sin id, alta; con id, edición del ejercicio propio. */
export interface EjercicioFormDatos {
  id?: string;
}

/**
 * Etapa 9a-2: alta o edición de un ejercicio propio del gimnasio (Dueño). El equipamiento se limita a lo que el
 * gimnasio tiene marcado más lo siempre disponible. El servidor valida todo de nuevo (nombre único, límites, zona).
 */
@Component({
  selector: 'app-ejercicio-form-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule,
    MatProgressBarModule, MatSelectModule],
  template: `
    <h2 mat-dialog-title>{{ datos.id ? 'Editar ejercicio' : 'Nuevo ejercicio' }}</h2>
    <mat-dialog-content>
      @if (cargando()) { <mat-progress-bar mode="indeterminate" /> }
      <form class="formulario" [formGroup]="form" (ngSubmit)="guardar()" id="form-ejercicio">
        <mat-form-field><mat-label>Nombre</mat-label>
          <input matInput formControlName="nombre" [maxlength]="limites.nombre" />
          @if (form.controls.nombre.hasError('required')) { <mat-error>El nombre es obligatorio.</mat-error> }
        </mat-form-field>
        <div class="dos">
          <mat-form-field><mat-label>Zona</mat-label>
            <mat-select formControlName="zonaObjetivo">
              @for (z of zonas(); track z) { <mat-option [value]="z">{{ z }}</mat-option> }
            </mat-select>
            @if (form.controls.zonaObjetivo.hasError('required')) { <mat-error>Elegí una zona.</mat-error> }
          </mat-form-field>
          <mat-form-field><mat-label>Equipamiento</mat-label>
            <mat-select formControlName="tipoEquipamientoId">
              @for (t of tipos(); track t.tipoEquipamientoId) { <mat-option [value]="t.tipoEquipamientoId">{{ t.nombre }}</mat-option> }
            </mat-select>
            <mat-hint>Solo lo que el gimnasio tiene declarado</mat-hint>
            @if (form.controls.tipoEquipamientoId.hasError('required')) { <mat-error>Elegí el equipamiento.</mat-error> }
          </mat-form-field>
        </div>
        <mat-form-field><mat-label>Músculo principal</mat-label>
          <input matInput formControlName="musculoPrincipal" [maxlength]="limites.nombre" />
          @if (form.controls.musculoPrincipal.hasError('required')) { <mat-error>El músculo principal es obligatorio.</mat-error> }
        </mat-form-field>
        <div class="dos">
          <mat-form-field><mat-label>Tipo de medida</mat-label>
            <mat-select formControlName="tipoMedida">
              <mat-option value="Repeticiones">Repeticiones</mat-option>
              <mat-option value="Segundos">Segundos</mat-option>
            </mat-select>
          </mat-form-field>
          <mat-form-field><mat-label>Dificultad ({{ limites.dificultadMin }} a {{ limites.dificultadMax }})</mat-label>
            <mat-select formControlName="dificultad">
              @for (n of dificultades; track n) { <mat-option [value]="n">{{ n }}</mat-option> }
            </mat-select>
          </mat-form-field>
        </div>
        <mat-form-field><mat-label>Preparación (opcional)</mat-label>
          <textarea matInput formControlName="preparacion" rows="2" [maxlength]="limites.textoLibre"></textarea>
        </mat-form-field>
        <mat-form-field><mat-label>Ejecución</mat-label>
          <textarea matInput formControlName="ejecucion" rows="3" [maxlength]="limites.textoLibre"></textarea>
          @if (form.controls.ejecucion.hasError('required')) { <mat-error>Describí la ejecución.</mat-error> }
        </mat-form-field>
      </form>
      @if (error()) { <div class="estado error" role="alert"><mat-icon>error_outline</mat-icon><span>{{ error() }}</span></div> }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close type="button">Cancelar</button>
      <button mat-flat-button type="submit" form="form-ejercicio" [disabled]="guardando() || cargando()">Guardar</button>
    </mat-dialog-actions>
  `,
  styles: `
    .formulario { display: grid; gap: 4px; padding-top: 8px; }
    .dos { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 12px; }
  `,
})
export class EjercicioFormDialog {
  private readonly api = inject(EjerciciosApi);
  private readonly equipamiento = inject(EquipamientoApi);
  private readonly ref = inject(MatDialogRef<EjercicioFormDialog, EjercicioDetalle>);
  readonly datos = inject<EjercicioFormDatos>(MAT_DIALOG_DATA);

  readonly limites = LIMITES_EJERCICIO;
  readonly dificultades = Array.from({ length: LIMITES_EJERCICIO.dificultadMax }, (_, i) => i + LIMITES_EJERCICIO.dificultadMin);
  readonly zonas = signal<string[]>([]);
  readonly tipos = signal<EquipamientoItem[]>([]);
  readonly cargando = signal(true);
  readonly guardando = signal(false);
  readonly error = signal<string | null>(null);

  readonly form = inject(NonNullableFormBuilder).group({
    nombre: ['', [Validators.required, Validators.maxLength(LIMITES_EJERCICIO.nombre)]],
    zonaObjetivo: ['', Validators.required],
    tipoEquipamientoId: ['', Validators.required],
    musculoPrincipal: ['', [Validators.required, Validators.maxLength(LIMITES_EJERCICIO.nombre)]],
    tipoMedida: ['Repeticiones' as TipoMedida],
    dificultad: [1],
    preparacion: ['', Validators.maxLength(LIMITES_EJERCICIO.textoLibre)],
    ejecucion: ['', [Validators.required, Validators.maxLength(LIMITES_EJERCICIO.textoLibre)]],
  });

  constructor() {
    forkJoin({ zonas: this.api.zonas(), equipamiento: this.equipamiento.obtener() }).subscribe({
      next: ({ zonas, equipamiento }) => {
        this.zonas.set(zonas);
        this.tipos.set(tiposElegibles(equipamiento));
        if (this.datos.id) this.cargarEjercicio(this.datos.id);
        else this.cargando.set(false);
      },
      error: (e) => { this.error.set(mensajeDeError(e, 'No se pudieron cargar las opciones.')); this.cargando.set(false); },
    });
  }

  private cargarEjercicio(id: string): void {
    this.api.obtener(id).subscribe({
      next: (e) => {
        // El tipo actual se conserva aunque el gimnasio ya no lo tenga marcado (el servidor lo permite al editar).
        if (e.tipoEquipamientoId && !this.tipos().some((t) => t.tipoEquipamientoId === e.tipoEquipamientoId)) {
          this.tipos.update((l) => [...l, { tipoEquipamientoId: e.tipoEquipamientoId!, clave: '', nombre: e.tipoEquipamiento ?? e.equipamiento,
            siempreDisponible: false, propio: false, marcado: false, cantidad: null }]);
        }
        this.form.patchValue({
          nombre: e.nombre, zonaObjetivo: e.zonaObjetivo, tipoEquipamientoId: e.tipoEquipamientoId ?? '',
          musculoPrincipal: e.musculoPrincipal, tipoMedida: e.tipoMedida ?? 'Repeticiones', dificultad: e.dificultad,
          preparacion: e.preparacion, ejecucion: e.ejecucion,
        });
        this.cargando.set(false);
      },
      error: (err) => { this.error.set(mensajeDeError(err, 'No se pudo cargar el ejercicio.')); this.cargando.set(false); },
    });
  }

  guardar(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const v = this.form.getRawValue();
    const dto: EjercicioGimnasioRequest = { ...v, preparacion: v.preparacion || null };
    this.guardando.set(true);
    this.error.set(null);
    const pedido = this.datos.id ? this.api.editar(this.datos.id, dto) : this.api.crear(dto);
    pedido.subscribe({
      next: (e) => this.ref.close(e),
      error: (e) => { this.error.set(mensajeDeError(e, 'No se pudo guardar el ejercicio.')); this.guardando.set(false); },
    });
  }
}
