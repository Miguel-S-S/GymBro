import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { AccesoApi } from '../../core/api/acceso.api';
import { SolicitudVinculacion } from '../../core/models/acceso.models';
import { mensajeDeError } from '../../core/util/api-error';

/** Dueño de Gimnasio: valida por DNI los pedidos de socios que quieren unirse. */
@Component({
  selector: 'app-vinculaciones',
  imports: [DatePipe, MatTableModule, MatButtonModule, MatIconModule, MatProgressBarModule],
  template: `
    <div class="encabezado">
      <div><h1>Solicitudes de vinculación</h1><p class="subtitulo">{{ solicitudes().length }} pendientes</p></div>
    </div>
    <p class="ayuda">Verificá que el DNI informado coincida con el de la persona antes de aprobar.</p>

    @if (cargando()) {
      <mat-progress-bar mode="indeterminate" />
    } @else if (solicitudes().length === 0) {
      <div class="estado"><mat-icon>how_to_reg</mat-icon><span>No hay solicitudes pendientes.</span></div>
    } @else {
      <div class="tabla-contenedor">
        <table mat-table [dataSource]="solicitudes()">
          <ng-container matColumnDef="socio"><th mat-header-cell *matHeaderCellDef>Socio</th><td mat-cell *matCellDef="let s">{{ s.nombreSocio }}</td></ng-container>
          <ng-container matColumnDef="dni"><th mat-header-cell *matHeaderCellDef>DNI</th><td mat-cell *matCellDef="let s">{{ s.dni }}</td></ng-container>
          <ng-container matColumnDef="fecha"><th mat-header-cell *matHeaderCellDef>Pedido</th><td mat-cell *matCellDef="let s">{{ s.fechaAlta | date: 'dd/MM/yyyy HH:mm' }}</td></ng-container>
          <ng-container matColumnDef="acciones">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let s" class="numero">
              <button mat-button [disabled]="procesando() === s.id" (click)="resolver(s, false)">Rechazar</button>
              <button mat-flat-button [disabled]="procesando() === s.id" (click)="resolver(s, true)">Aprobar</button>
            </td>
          </ng-container>
          <tr mat-header-row *matHeaderRowDef="columnas"></tr>
          <tr mat-row *matRowDef="let s; columns: columnas"></tr>
        </table>
      </div>
    }
  `,
  styles: `.ayuda { color: var(--mat-sys-on-surface-variant); }`,
})
export class Vinculaciones {
  private readonly api = inject(AccesoApi);
  private readonly snack = inject(MatSnackBar);

  readonly columnas = ['socio', 'dni', 'fecha', 'acciones'];
  readonly solicitudes = signal<SolicitudVinculacion[]>([]);
  readonly cargando = signal(true);
  readonly procesando = signal<string | null>(null);

  constructor() {
    this.cargar();
  }

  cargar(): void {
    this.api.solicitudesPendientes().subscribe({
      next: (s) => { this.solicitudes.set(s); this.cargando.set(false); },
      error: (e) => { this.snack.open(mensajeDeError(e), 'Cerrar', { duration: 5000 }); this.cargando.set(false); },
    });
  }

  resolver(s: SolicitudVinculacion, aprobar: boolean): void {
    this.procesando.set(s.id);
    this.api.resolverSolicitud(s.id, aprobar, aprobar ? undefined : 'Rechazada por el gimnasio').subscribe({
      next: () => {
        this.solicitudes.update((l) => l.filter((x) => x.id !== s.id));
        this.snack.open(aprobar ? `${s.nombreSocio} ya es socio del gimnasio` : 'Solicitud rechazada', 'Cerrar', { duration: 4000 });
        this.procesando.set(null);
      },
      error: (e) => { this.snack.open(mensajeDeError(e), 'Cerrar', { duration: 6000 }); this.procesando.set(null); },
    });
  }
}