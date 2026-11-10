import { Component, effect, inject, input, signal } from '@angular/core';
import {
  AbstractControl,
  FormArray,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router, RouterLink } from '@angular/router';
import { SesionesApi } from '../../core/api/sesiones.api';
import { SociosApi } from '../../core/api/socios.api';
import { EjercicioResumen } from '../../core/models/ejercicio.models';
import { mensajeDeError } from '../../core/util/api-error';
import { aDatetimeLocal } from '../../core/util/formato';
import { EjercicioSelector } from '../ejercicios/ejercicio-selector';
import { SesionFormValue, construirRegistrarSesionRequest } from './sesion-request';

type SerieForm = FormGroup<{
  pesoLevantadoKg: FormControl<number | null>;
  repeticiones: FormControl<number | null>;
  rir: FormControl<number | null>;
  esCalentamiento: FormControl<boolean>;
  tiempoDescansoSegundos: FormControl<number | null>;
}>;

type EjercicioForm = FormGroup<{
  ejercicio: FormControl<EjercicioResumen | null>;
  observaciones: FormControl<string | null>;
  series: FormArray<SerieForm>;
}>;

/** Fin, si se informa, no puede ser anterior al inicio. */
function finPosteriorAInicio(grupo: AbstractControl): ValidationErrors | null {
  const inicio = grupo.get('inicio')?.value as string | null;
  const fin = grupo.get('fin')?.value as string | null;
  if (!inicio || !fin) return null;
  return new Date(fin) < new Date(inicio) ? { finAnterior: true } : null;
}

function nuevaSerie(previa?: SerieForm): SerieForm {
  // Para agilizar la carga, una serie nueva copia peso, repeticiones y descanso de la anterior.
  const p = previa?.getRawValue();
  return new FormGroup({
    pesoLevantadoKg: new FormControl<number | null>(p?.pesoLevantadoKg ?? null, [Validators.required, Validators.min(0)]),
    repeticiones: new FormControl<number | null>(p?.repeticiones ?? null, [Validators.required, Validators.min(1)]),
    rir: new FormControl<number | null>(null, [Validators.min(0), Validators.max(5)]),
    esCalentamiento: new FormControl(false, { nonNullable: true }),
    tiempoDescansoSegundos: new FormControl<number | null>(p?.tiempoDescansoSegundos ?? null, [Validators.min(0)]),
  });
}

function nuevoEjercicio(): EjercicioForm {
  return new FormGroup({
    ejercicio: new FormControl<EjercicioResumen | null>(null, Validators.required),
    observaciones: new FormControl<string | null>(null, Validators.maxLength(500)),
    series: new FormArray<SerieForm>([nuevaSerie()], Validators.minLength(1)),
  });
}

@Component({
  selector: 'app-sesion-registro',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatCheckboxModule,
    MatTooltipModule,
    MatProgressBarModule,
    EjercicioSelector,
  ],
  template: `
    <a mat-button [routerLink]="['/socios', id()]" class="volver"><mat-icon>arrow_back</mat-icon> Volver al socio</a>

    <div class="encabezado">
      <div>
        <h1>Registrar sesión</h1>
        <p class="subtitulo">{{ nombreSocio() ?? '…' }}</p>
      </div>
    </div>

    <p class="aviso">
      <mat-icon>info</mat-icon>
      <span>
        Se registra lo que el socio ejecutó realmente, serie por serie. La sesión queda como <strong>libre</strong>
        (sin prescripción asociada) porque la API todavía no expone las prescripciones.
      </span>
    </p>

    <form [formGroup]="form" (ngSubmit)="guardar()">
      <section class="tarjeta">
        <h2>Datos generales</h2>
        <div class="fila-campos">
          <mat-form-field>
            <mat-label>Inicio</mat-label>
            <input matInput type="datetime-local" formControlName="inicio" />
            @if (form.controls.inicio.hasError('required')) { <mat-error>Indicá el inicio.</mat-error> }
          </mat-form-field>
          <mat-form-field>
            <mat-label>Fin</mat-label>
            <input matInput type="datetime-local" formControlName="fin" />
            <mat-hint>Opcional</mat-hint>
          </mat-form-field>
        </div>
        <div class="fila-campos metricas">
          <mat-form-field>
            <mat-label>Calorías</mat-label>
            <input matInput type="number" min="0" formControlName="caloriasQuemadas" />
            <span matTextSuffix>kcal</span>
            <mat-hint>Opcional</mat-hint>
          </mat-form-field>
          <mat-form-field>
            <mat-label>FC máxima</mat-label>
            <input matInput type="number" min="0" formControlName="frecuenciaCardiacaMaxima" />
            <span matTextSuffix>lpm</span>
            <mat-hint>Opcional</mat-hint>
          </mat-form-field>
          <mat-form-field>
            <mat-label>FC promedio</mat-label>
            <input matInput type="number" min="0" formControlName="frecuenciaCardiacaPromedio" />
            <span matTextSuffix>lpm</span>
            <mat-hint>Opcional</mat-hint>
          </mat-form-field>
        </div>
        @if (form.hasError('finAnterior')) {
          <p class="error">El fin no puede ser anterior al inicio.</p>
        }
      </section>

      @for (ej of ejercicios.controls; track ej; let i = $index) {
        <section class="tarjeta ejercicio" [formGroup]="ej">
          <div class="cabecera-ejercicio">
            <span class="orden">{{ i + 1 }}</span>
            <app-ejercicio-selector formControlName="ejercicio" class="selector" />
            <button mat-icon-button type="button" (click)="quitarEjercicio(i)" matTooltip="Quitar ejercicio" aria-label="Quitar ejercicio">
              <mat-icon>delete</mat-icon>
            </button>
          </div>
          @if (ej.controls.ejercicio.touched && ej.controls.ejercicio.invalid) {
            <p class="error">Elegí un ejercicio de la lista.</p>
          }

          <div class="series" formArrayName="series">
            <div class="serie encabezado-series">
              <span>#</span><span>Peso (kg)</span><span>Reps</span><span>RIR</span><span>Descanso (s)</span><span>Calent.</span><span></span>
            </div>
            @for (s of ej.controls.series.controls; track s; let j = $index) {
              <div class="serie" [formGroup]="s">
                <span class="num">{{ j + 1 }}</span>
                <mat-form-field><input matInput type="number" min="0" step="0.5" formControlName="pesoLevantadoKg" aria-label="Peso" /></mat-form-field>
                <mat-form-field><input matInput type="number" min="1" formControlName="repeticiones" aria-label="Repeticiones" /></mat-form-field>
                <mat-form-field><input matInput type="number" min="0" max="5" formControlName="rir" aria-label="RIR" /></mat-form-field>
                <mat-form-field><input matInput type="number" min="0" step="5" formControlName="tiempoDescansoSegundos" aria-label="Descanso en segundos" /></mat-form-field>
                <mat-checkbox formControlName="esCalentamiento" aria-label="Serie de calentamiento" />
                <button mat-icon-button type="button" (click)="quitarSerie(ej, j)" [disabled]="ej.controls.series.length === 1" aria-label="Quitar serie">
                  <mat-icon>remove_circle_outline</mat-icon>
                </button>
              </div>
            }
            @if (ej.controls.series.touched && ej.controls.series.invalid) {
              <p class="error">Revisá las series: peso ≥ 0, repeticiones ≥ 1, RIR entre 0 y 5 y descanso ≥ 0.</p>
            }
            <button mat-button type="button" (click)="agregarSerie(ej)"><mat-icon>add</mat-icon> Serie</button>
          </div>

          <mat-form-field class="observaciones">
            <mat-label>Observaciones (opcional)</mat-label>
            <textarea matInput rows="2" formControlName="observaciones" maxlength="500"></textarea>
          </mat-form-field>
        </section>
      }

      <div class="acciones">
        <button mat-stroked-button type="button" (click)="agregarEjercicio()"><mat-icon>add</mat-icon> Agregar ejercicio</button>
        <span class="espacio"></span>
        <a mat-button [routerLink]="['/socios', id()]">Cancelar</a>
        <button mat-flat-button type="submit" [disabled]="guardando()">
          <mat-icon>save</mat-icon> Guardar sesión
        </button>
      </div>
      @if (guardando()) { <mat-progress-bar mode="indeterminate" /> }
      @if (error()) { <p class="error">{{ error() }}</p> }
    </form>
  `,
  styles: `
    .volver { margin: -8px 0 8px -12px; }
    h2 { font: var(--mat-sys-title-medium); margin: 0 0 12px; }
    .tarjeta { margin-bottom: 16px; }
    .metricas { margin-top: 12px; }
    .error { color: var(--mat-sys-error); font: var(--mat-sys-body-small); margin: 4px 0 8px; }
    .cabecera-ejercicio { display: flex; align-items: flex-start; gap: 12px; }
    .selector { flex: 1; }
    .orden {
      display: grid; place-items: center; flex: 0 0 32px; height: 32px; margin-top: 12px; border-radius: 50%;
      background: var(--mat-sys-primary); color: var(--mat-sys-on-primary); font: var(--mat-sys-label-large);
    }
    .series { margin: 8px 0 12px; overflow-x: auto; }
    .serie {
      display: grid; grid-template-columns: 28px repeat(4, minmax(80px, 1fr)) 64px 48px;
      gap: 8px; align-items: center; min-width: 520px;
      mat-form-field { width: 100%; }
    }
    .encabezado-series { font: var(--mat-sys-label-medium); color: var(--mat-sys-on-surface-variant); padding-bottom: 4px; }
    .num { text-align: center; font: var(--mat-sys-label-large); }
    .observaciones { width: 100%; }
    .acciones { display: flex; flex-wrap: wrap; gap: 8px; align-items: center; margin: 8px 0 16px; }
    .espacio { flex: 1; }
  `,
})
export class SesionRegistro {
  private readonly sesiones = inject(SesionesApi);
  private readonly socios = inject(SociosApi);
  private readonly router = inject(Router);
  private readonly snack = inject(MatSnackBar);

  /** Parámetro de ruta :id (id del socio). */
  readonly id = input.required<string>();

  readonly nombreSocio = signal<string | null>(null);
  readonly guardando = signal(false);
  readonly error = signal<string | null>(null);

  readonly form = new FormGroup(
    {
      inicio: new FormControl<string>(aDatetimeLocal(new Date()), { nonNullable: true, validators: Validators.required }),
      fin: new FormControl<string | null>(null),
      caloriasQuemadas: new FormControl<number | null>(null, Validators.min(0)),
      frecuenciaCardiacaMaxima: new FormControl<number | null>(null, Validators.min(0)),
      frecuenciaCardiacaPromedio: new FormControl<number | null>(null, Validators.min(0)),
      ejercicios: new FormArray<EjercicioForm>([nuevoEjercicio()]),
    },
    { validators: finPosteriorAInicio },
  );

  get ejercicios(): FormArray<EjercicioForm> {
    return this.form.controls.ejercicios;
  }

  constructor() {
    effect(() => {
      this.socios.obtener(this.id()).subscribe({
        next: (s) => this.nombreSocio.set(s.nombreCompleto),
        error: () => this.nombreSocio.set('Socio no encontrado'),
      });
    });
  }

  agregarEjercicio(): void {
    this.ejercicios.push(nuevoEjercicio());
  }

  quitarEjercicio(i: number): void {
    this.ejercicios.removeAt(i);
  }

  agregarSerie(ej: EjercicioForm): void {
    const series = ej.controls.series;
    series.push(nuevaSerie(series.at(series.length - 1)));
  }

  quitarSerie(ej: EjercicioForm, j: number): void {
    ej.controls.series.removeAt(j);
  }

  guardar(): void {
    this.error.set(null);
    if (this.ejercicios.length === 0) {
      this.error.set('Agregá al menos un ejercicio.');
      return;
    }
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.error.set('Hay campos con errores. Revisá el formulario.');
      return;
    }

    const request = construirRegistrarSesionRequest(this.id(), this.form.getRawValue() as SesionFormValue);
    this.guardando.set(true);
    this.sesiones.registrar(request).subscribe({
      next: (sesion) => {
        this.snack.open('Sesión registrada', 'Cerrar', { duration: 3000 });
        this.router.navigate(['/sesiones', sesion.id]);
      },
      error: (e) => {
        this.guardando.set(false);
        this.error.set(mensajeDeError(e, 'No se pudo registrar la sesión.'));
      },
    });
  }
}
