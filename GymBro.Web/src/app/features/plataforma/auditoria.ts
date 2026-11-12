import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { AccesoApi } from '../../core/api/acceso.api';
import { AuditLog, Gimnasio } from '../../core/models/acceso.models';
import { mensajeDeError } from '../../core/util/api-error';

/** Administrador de la Plataforma: eventos registrados, filtrables por fecha, gimnasio, acción y entidad. */
@Component({
  selector: 'app-auditoria',
  imports: [DatePipe, ReactiveFormsModule, MatTableModule, MatIconModule, MatProgressBarModule, MatButtonModule,
    MatFormFieldModule, MatInputModule, MatSelectModule],
  template: `
    <div class="encabezado">
      <div><h1>Auditoría</h1><p class="subtitulo">{{ registros().length }} eventos (máx. 200, los más recientes)</p></div>
    </div>

    <form class="filtros" [formGroup]="form" (ngSubmit)="buscar()">
      <mat-form-field><mat-label>Desde</mat-label><input matInput type="date" formControlName="desde" /></mat-form-field>
      <mat-form-field><mat-label>Hasta</mat-label><input matInput type="date" formControlName="hasta" /></mat-form-field>
      <mat-form-field>
        <mat-label>Gimnasio</mat-label>
        <mat-select formControlName="gimnasioId">
          <mat-option value="">Todos</mat-option>
          @for (g of gimnasios(); track g.id) { <mat-option [value]="g.id">{{ g.nombre }}</mat-option> }
        </mat-select>
      </mat-form-field>
      <mat-form-field><mat-label>Acción</mat-label><input matInput formControlName="accion" placeholder="ALTA_GIMNASIO" /></mat-form-field>
      <mat-form-field><mat-label>Entidad</mat-label><input matInput formControlName="entidad" placeholder="Gimnasio" /></mat-form-field>
      <div class="botones">
        <button mat-flat-button type="submit" [disabled]="cargando()"><mat-icon>search</mat-icon> Buscar</button>
        <button mat-button type="button" (click)="limpiar()">Limpiar</button>
      </div>
    </form>

    @if (cargando()) {
      <mat-progress-bar mode="indeterminate" />
    } @else if (error()) {
      <div class="estado error"><mat-icon>cloud_off</mat-icon><span>{{ error() }}</span></div>
    } @else if (registros().length === 0) {
      <div class="estado"><mat-icon>fact_check</mat-icon><span>Sin eventos para los filtros elegidos.</span></div>
    } @else {
      <div class="tabla-contenedor">
        <table mat-table [dataSource]="registros()">
          <ng-container matColumnDef="fecha"><th mat-header-cell *matHeaderCellDef>Fecha</th><td mat-cell *matCellDef="let r">{{ r.fecha | date: 'dd/MM/yyyy HH:mm' }}</td></ng-container>
          <ng-container matColumnDef="rol"><th mat-header-cell *matHeaderCellDef>Rol</th><td mat-cell *matCellDef="let r">{{ r.rol ?? '—' }}</td></ng-container>
          <ng-container matColumnDef="accion"><th mat-header-cell *matHeaderCellDef>Acción</th><td mat-cell *matCellDef="let r">{{ r.accion }}</td></ng-container>
          <ng-container matColumnDef="entidad"><th mat-header-cell *matHeaderCellDef>Entidad</th><td mat-cell *matCellDef="let r">{{ r.entidad }}</td></ng-container>
          <ng-container matColumnDef="detalle"><th mat-header-cell *matHeaderCellDef>Detalle</th><td mat-cell *matCellDef="let r">{{ r.detalle ?? '' }}</td></ng-container>
          <tr mat-header-row *matHeaderRowDef="columnas"></tr>
          <tr mat-row *matRowDef="let r; columns: columnas"></tr>
        </table>
      </div>
    }
  `,
  styles: `
    .filtros { display: flex; flex-wrap: wrap; gap: 12px; align-items: flex-start; margin-bottom: 16px; }
    .filtros mat-form-field { width: 170px; }
    .botones { display: flex; gap: 8px; padding-top: 4px; }
  `,
})
export class Auditoria {
  private readonly api = inject(AccesoApi);

  readonly columnas = ['fecha', 'rol', 'accion', 'entidad', 'detalle'];
  readonly registros = signal<AuditLog[]>([]);
  readonly gimnasios = signal<Gimnasio[]>([]);
  readonly cargando = signal(true);
  readonly error = signal<string | null>(null);

  readonly form = inject(NonNullableFormBuilder).group({
    desde: [''],
    hasta: [''],
    gimnasioId: [''],
    accion: [''],
    entidad: [''],
  });

  constructor() {
    this.api.listarGimnasios().subscribe({ next: (g) => this.gimnasios.set(g), error: () => {} });
    this.buscar();
  }

  buscar(): void {
    this.cargando.set(true);
    this.error.set(null);
    this.api.auditoria({ ...this.form.getRawValue(), cantidad: 200 }).subscribe({
      next: (r) => { this.registros.set(r); this.cargando.set(false); },
      error: (e) => { this.error.set(mensajeDeError(e, 'No se pudo cargar la auditoría.')); this.cargando.set(false); },
    });
  }

  limpiar(): void {
    this.form.reset();
    this.buscar();
  }
}