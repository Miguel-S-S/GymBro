import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { AccesoApi } from '../../core/api/acceso.api';
import { AuditLog } from '../../core/models/acceso.models';
import { mensajeDeError } from '../../core/util/api-error';

/** Administrador de la Plataforma: últimas acciones registradas en el sistema. */
@Component({
  selector: 'app-auditoria',
  imports: [DatePipe, MatTableModule, MatIconModule, MatProgressBarModule],
  template: `
    <div class="encabezado">
      <div><h1>Auditoría</h1><p class="subtitulo">Últimos {{ registros().length }} eventos</p></div>
    </div>
    @if (cargando()) {
      <mat-progress-bar mode="indeterminate" />
    } @else if (error()) {
      <div class="estado error"><mat-icon>cloud_off</mat-icon><span>{{ error() }}</span></div>
    } @else if (registros().length === 0) {
      <div class="estado"><mat-icon>fact_check</mat-icon><span>Sin eventos registrados.</span></div>
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
})
export class Auditoria {
  private readonly api = inject(AccesoApi);
  readonly columnas = ['fecha', 'rol', 'accion', 'entidad', 'detalle'];
  readonly registros = signal<AuditLog[]>([]);
  readonly cargando = signal(true);
  readonly error = signal<string | null>(null);

  constructor() {
    this.api.auditoria(200).subscribe({
      next: (r) => { this.registros.set(r); this.cargando.set(false); },
      error: (e) => { this.error.set(mensajeDeError(e, 'No se pudo cargar la auditoría.')); this.cargando.set(false); },
    });
  }
}