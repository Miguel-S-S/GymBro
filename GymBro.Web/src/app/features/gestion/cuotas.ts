import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormControl, NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AccesoApi } from '../../core/api/acceso.api';
import { AsistenciaApi } from '../../core/api/asistencia.api';
import { AuthService } from '../../core/auth/auth.service';
import { Gimnasio } from '../../core/models/acceso.models';
import { Cuota } from '../../core/models/asistencia.models';
import { mensajeDeError } from '../../core/util/api-error';

/** Año y mes de hoy (valor inicial del formulario). */
function periodoActual(hoy = new Date()): { anio: number; mes: number } {
  return { anio: hoy.getFullYear(), mes: hoy.getMonth() + 1 };
}

/**
 * Etapa 11: cuotas del mes y morosos. El Dueño genera las cuotas del período (idempotente) y registra pagos a mano; el
 * Administrador de la Plataforma lee entre gimnasios sin escribir. No hay pasarela de pago. "Vencida" es un dato
 * derivado que devuelve la API (pendiente con vencimiento anterior a hoy).
 */
@Component({
  selector: 'app-cuotas',
  imports: [CurrencyPipe, DatePipe, ReactiveFormsModule, MatButtonModule, MatButtonToggleModule, MatFormFieldModule,
    MatIconModule, MatInputModule, MatProgressBarModule, MatSelectModule],
  template: `
    <div class="encabezado"><h1>Cuotas</h1></div>

    @if (esPlataforma) {
      <mat-form-field class="selector">
        <mat-label>Gimnasio</mat-label>
        <mat-select [formControl]="gimnasioElegido" (selectionChange)="cargar()">
          <mat-option [value]="null">Todos los gimnasios</mat-option>
          @for (g of gimnasios(); track g.id) { <mat-option [value]="g.id">{{ g.nombre }}</mat-option> }
        </mat-select>
      </mat-form-field>
      <p class="ayuda"><mat-icon>visibility</mat-icon> Solo lectura: las cuotas las gestiona el Dueño de cada gimnasio.</p>
    }

    <div class="fila">
      <mat-button-toggle-group [value]="vista()" (change)="vista.set($event.value); cargar()" aria-label="Vista">
        <mat-button-toggle value="mes">Cuotas del mes</mat-button-toggle>
        <mat-button-toggle value="morosos">Morosos</mat-button-toggle>
      </mat-button-toggle-group>
      @if (vista() === 'mes') {
        <form class="fila" [formGroup]="periodo" (ngSubmit)="cargar()">
          <mat-form-field class="corto"><mat-label>Mes</mat-label><input matInput type="number" min="1" max="12" formControlName="mes" /></mat-form-field>
          <mat-form-field class="corto"><mat-label>Año</mat-label><input matInput type="number" formControlName="anio" /></mat-form-field>
          <button mat-stroked-button type="submit">Ver</button>
        </form>
      }
    </div>

    @if (!esPlataforma && vista() === 'mes') {
      <form class="tarjeta generar" [formGroup]="generar" (ngSubmit)="generarCuotas()">
        <h2>Generar cuotas de {{ periodo.value.mes }}/{{ periodo.value.anio }} para los socios activos</h2>
        <mat-form-field><mat-label>Monto</mat-label><input matInput type="number" min="0.01" step="0.01" formControlName="monto" /></mat-form-field>
        <mat-form-field><mat-label>Vencimiento</mat-label><input matInput type="date" formControlName="vencimiento" /></mat-form-field>
        <button mat-flat-button type="submit" [disabled]="generar.invalid || ocupado()">Generar</button>
        <p class="ayuda">Si un socio ya tiene la cuota del período, no se duplica.</p>
      </form>
    }

    @if (cargando()) {
      <mat-progress-bar mode="indeterminate" />
    } @else if (error()) {
      <div class="estado error"><mat-icon>error_outline</mat-icon><span>{{ error() }}</span></div>
    } @else {
      <p class="total">{{ cuotas().length }} {{ cuotas().length === 1 ? 'cuota' : 'cuotas' }}
        @if (vista() === 'mes') { · {{ pendientes() }} pendientes · {{ vencidas() }} vencidas }</p>
      @for (c of cuotas(); track c.id) {
        <div class="item">
          <div>
            <div>{{ c.socioNombre }}</div>
            <div class="meta">{{ c.mes.toString().padStart(2, '0') }}/{{ c.anio }} · {{ c.monto | currency: 'ARS' : 'symbol-narrow' : '1.2-2' }}
              · vence {{ c.vencimiento | date: 'dd/MM/yyyy' }}@if (esPlataforma) { · {{ c.gimnasioNombre }} }</div>
          </div>
          <span class="estado" [class.vencida]="c.vencida" [class.pagada]="c.estado === 'Pagada'">
            @if (c.estado === 'Pagada') { Pagada {{ c.fechaPago | date: 'dd/MM' }} }
            @else if (c.vencida) { Vencida ({{ c.diasVencida }} d) }
            @else { Pendiente }
          </span>
          @if (!esPlataforma && c.estado === 'Pendiente') {
            <button mat-stroked-button type="button" [disabled]="ocupado()" (click)="registrarPago(c)">Registrar pago</button>
          }
        </div>
      } @empty {
        <p class="ayuda">{{ vista() === 'mes' ? 'No hay cuotas generadas en este período.' : 'No hay cuotas vencidas.' }}</p>
      }
    }
  `,
  styles: `
    .selector { width: 100%; max-width: 420px; }
    .fila { display: flex; flex-wrap: wrap; align-items: center; gap: 12px; margin-bottom: 8px; }
    .corto { width: 96px; }
    .tarjeta { border: 1px solid var(--mat-sys-outline-variant); border-radius: 16px; padding: 16px; margin: 8px 0 16px;
      display: flex; flex-wrap: wrap; align-items: center; gap: 12px; }
    .tarjeta h2 { flex-basis: 100%; font: var(--mat-sys-title-small); margin: 0; }
    .ayuda { display: flex; align-items: center; gap: 8px; color: var(--mat-sys-on-surface-variant); flex-basis: 100%; }
    .total { font: var(--mat-sys-title-small); }
    .item { display: grid; grid-template-columns: 1fr auto auto; align-items: center; gap: 12px; padding: 8px 0; border-top: 1px solid var(--mat-sys-outline-variant); }
    .meta { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); }
    .estado { font: var(--mat-sys-label-large); }
    .estado.vencida { color: var(--mat-sys-error); }
    .estado.pagada { color: var(--mat-sys-primary); }
  `,
})
export class Cuotas {
  private readonly api = inject(AsistenciaApi);
  private readonly snack = inject(MatSnackBar);
  private readonly fb = inject(NonNullableFormBuilder);

  readonly esPlataforma = inject(AuthService).tieneRol('SuperAdministrador');
  readonly gimnasioElegido = new FormControl<string | null>(null);
  readonly gimnasios = signal<Gimnasio[]>([]);
  readonly vista = signal<'mes' | 'morosos'>('mes');
  readonly cuotas = signal<Cuota[]>([]);
  readonly cargando = signal(true);
  readonly ocupado = signal(false);
  readonly error = signal<string | null>(null);

  readonly periodo = this.fb.group({
    anio: [periodoActual().anio, [Validators.required, Validators.min(2000), Validators.max(2100)]],
    mes: [periodoActual().mes, [Validators.required, Validators.min(1), Validators.max(12)]],
  });
  readonly generar = this.fb.group({
    monto: [0, [Validators.required, Validators.min(0.01)]],
    vencimiento: ['', Validators.required],
  });

  constructor() {
    if (this.esPlataforma) inject(AccesoApi).listarGimnasios().subscribe((l) => this.gimnasios.set(l));
    this.cargar();
  }

  pendientes(): number {
    return this.cuotas().filter((c) => c.estado === 'Pendiente').length;
  }

  vencidas(): number {
    return this.cuotas().filter((c) => c.vencida).length;
  }

  private get destino(): string | null {
    return this.esPlataforma ? this.gimnasioElegido.value : null;
  }

  cargar(): void {
    this.cargando.set(true);
    this.error.set(null);
    const { anio, mes } = this.periodo.getRawValue();
    const pedido = this.vista() === 'mes' ? this.api.cuotasDelPeriodo(anio, mes, this.destino) : this.api.morosos(this.destino);
    pedido.subscribe({
      next: (l) => { this.cuotas.set(l); this.cargando.set(false); },
      error: (e) => { this.error.set(mensajeDeError(e, 'No se pudieron cargar las cuotas.')); this.cargando.set(false); },
    });
  }

  generarCuotas(): void {
    if (this.generar.invalid || this.periodo.invalid) { this.generar.markAllAsTouched(); return; }
    const { anio, mes } = this.periodo.getRawValue();
    const { monto, vencimiento } = this.generar.getRawValue();
    this.ocupado.set(true);
    this.api.generarCuotas({ anio, mes, monto, vencimiento }).subscribe({
      next: (r) => {
        this.snack.open(`Cuotas creadas: ${r.creadas}. Ya existían: ${r.existentes}.`, 'Cerrar', { duration: 4000 });
        this.ocupado.set(false);
        this.cargar();
      },
      error: (e) => { this.snack.open(mensajeDeError(e), 'Cerrar', { duration: 5000 }); this.ocupado.set(false); },
    });
  }

  registrarPago(c: Cuota): void {
    this.ocupado.set(true);
    this.api.registrarPago(c.id).subscribe({
      next: (pagada) => {
        this.cuotas.update((l) => (this.vista() === 'morosos' ? l.filter((x) => x.id !== c.id) : l.map((x) => (x.id === c.id ? pagada : x))));
        this.snack.open('Pago registrado', 'Cerrar', { duration: 3000 });
        this.ocupado.set(false);
      },
      error: (e) => { this.snack.open(mensajeDeError(e), 'Cerrar', { duration: 5000 }); this.ocupado.set(false); },
    });
  }
}
