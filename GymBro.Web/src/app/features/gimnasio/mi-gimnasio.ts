import { Component, inject, signal } from '@angular/core';
import { FormControl, NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
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
import { AuthService } from '../../core/auth/auth.service';
import { Entrenador, Gimnasio } from '../../core/models/acceso.models';
import { SocioResumen } from '../../core/models/socio.models';
import { mensajeDeError } from '../../core/util/api-error';

/**
 * Dueño de Gimnasio: código de vinculación, política de revisión de rutinas y Personal Trainers.
 * Etapa 9a-2c (D1): el Administrador de la Plataforma elige el gimnasio, lo ve y cambia la política de revisión (auditado).
 * Las secciones de entrenadores y asignaciones se le muestran con un aviso (decisión P2 del autor): para la plataforma
 * esa gestión se hace por script (Etapa 9a-2b), porque sus endpoints son del Dueño y la lista de socios no le es visible.
 */
@Component({
  selector: 'app-mi-gimnasio',
  imports: [ReactiveFormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule,
    MatProgressBarModule, MatSelectModule, MatSlideToggleModule],
  template: `
    @if (esPlataforma) {
      <mat-form-field class="selector">
        <mat-label>Gimnasio</mat-label>
        <mat-select [formControl]="gimnasioElegido" (selectionChange)="cargar()">
          @for (g of gimnasios(); track g.id) { <mat-option [value]="g.id">{{ g.nombre }}</mat-option> }
        </mat-select>
      </mat-form-field>
    }

    @if (esPlataforma && !gimnasioElegido.value) {
      <div class="estado sin-gimnasio"><mat-icon>domain</mat-icon><span>Elegí un gimnasio para ver su configuración.</span></div>
    } @else if (gimnasio(); as g) {
      <div class="encabezado"><div><h1>{{ g.nombre }}</h1>
        <p class="subtitulo">Código para que los socios se vinculen: <code>{{ g.codigo }}</code></p></div></div>

      <section>
        <h2>Revisión de rutinas</h2>
        <mat-slide-toggle [checked]="g.requiereAprobacionRutinas" (change)="cambiarPolitica($event.checked)">
          Las rutinas generadas por la IA requieren revisión humana antes de llegar al socio
        </mat-slide-toggle>
      </section>

      <section class="entrenadores">
        <h2>Personal Trainers</h2>
        @if (esPlataforma) {
          <p class="aviso-script"><mat-icon>terminal</mat-icon>
            <span>La gestión de entrenadores del Administrador de la Plataforma se hace con el script <code>admin-usuario.sql</code>.</span></p>
        } @else {
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
        }
      </section>

      <section class="asignacion">
        <h2>Asignar socio a un entrenador</h2>
        @if (esPlataforma) {
          <p class="aviso-script"><mat-icon>terminal</mat-icon>
            <span>Las asignaciones del Administrador de la Plataforma se hacen con el script <code>admin-asignacion.sql</code>.</span></p>
        } @else {
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
        }
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
    .selector { width: 100%; max-width: 420px; }
    .aviso-script { display: flex; align-items: center; gap: 8px; color: var(--mat-sys-on-surface-variant); }
  `,
})
export class MiGimnasio {
  private readonly api = inject(AccesoApi);
  private readonly sociosApi = inject(SociosApi);
  private readonly snack = inject(MatSnackBar);
  private readonly fb = inject(NonNullableFormBuilder);

  readonly esPlataforma = inject(AuthService).tieneRol('SuperAdministrador');
  readonly gimnasioElegido = new FormControl<string | null>(null);
  readonly gimnasios = signal<Gimnasio[]>([]);
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
    if (this.esPlataforma) {
      this.api.listarGimnasios().subscribe({
        next: (l) => this.gimnasios.set(l),
        error: (e) => this.error.set(mensajeDeError(e, 'No se pudieron cargar los gimnasios.')),
      });
    } else {
      this.cargar();
      this.api.listarEntrenadores().subscribe((l) => this.entrenadores.set(l));
      this.sociosApi.listar().subscribe((l) => this.socios.set(l));
    }
  }

  /** Gimnasio que se envía: solo la plataforma lo indica; el Dueño usa el de su sesión. */
  private get destino(): string | null {
    return this.esPlataforma ? this.gimnasioElegido.value : null;
  }

  cargar(): void {
    this.gimnasio.set(null);
    this.error.set(null);
    this.api.miGimnasio(this.destino).subscribe({
      next: (g) => this.gimnasio.set(g),
      error: (e) => this.error.set(mensajeDeError(e, 'No se pudo cargar el gimnasio.')),
    });
  }

  cambiarPolitica(requiere: boolean): void {
    this.api.actualizarPoliticaRevision(requiere, this.destino).subscribe({
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
