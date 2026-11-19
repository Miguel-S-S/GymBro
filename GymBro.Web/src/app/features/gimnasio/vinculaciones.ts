import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { AccesoApi } from '../../core/api/acceso.api';
import { AuthService } from '../../core/auth/auth.service';
import { Gimnasio, SolicitudVinculacion } from '../../core/models/acceso.models';
import { mensajeDeError } from '../../core/util/api-error';

/**
 * Dueño de Gimnasio: valida por DNI los pedidos de socios que quieren unirse.
 * Etapa 9a-2c (D1): el Administrador de la Plataforma elige el gimnasio y resuelve igual que el Dueño (todo auditado).
 * Las solicitudes no contienen datos de salud (nombre, DNI declarado, estado y fecha).
 */
@Component({
  selector: 'app-vinculaciones',
  imports: [DatePipe, ReactiveFormsModule, MatTableModule, MatButtonModule, MatFormFieldModule, MatIconModule,
    MatProgressBarModule, MatSelectModule],
  template: `
    <div class="encabezado">
      <div><h1>Solicitudes de vinculación</h1>
        @if (!esPlataforma || gimnasio.value) { <p class="subtitulo">{{ solicitudes().length }} pendientes</p> }</div>
    </div>

    @if (esPlataforma) {
      <mat-form-field class="selector">
        <mat-label>Gimnasio</mat-label>
        <mat-select [formControl]="gimnasio" (selectionChange)="cargar()">
          @for (g of gimnasios(); track g.id) { <mat-option [value]="g.id">{{ g.nombre }}</mat-option> }
        </mat-select>
      </mat-form-field>
    }

    @if (esPlataforma && !gimnasio.value) {
      <div class="estado sin-gimnasio"><mat-icon>domain</mat-icon><span>Elegí un gimnasio para ver sus solicitudes.</span></div>
    } @else {
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
                <button mat-button class="rechazar" [disabled]="procesando() === s.id" (click)="resolver(s, false)">Rechazar</button>
                <button mat-flat-button class="aprobar" [disabled]="procesando() === s.id" (click)="resolver(s, true)">Aprobar</button>
              </td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="columnas"></tr>
            <tr mat-row *matRowDef="let s; columns: columnas"></tr>
          </table>
        </div>
      }
    }
  `,
  styles: `
    .ayuda { color: var(--mat-sys-on-surface-variant); }
    .selector { width: 100%; max-width: 420px; }
  `,
})
export class Vinculaciones {
  private readonly api = inject(AccesoApi);
  private readonly snack = inject(MatSnackBar);

  readonly esPlataforma = inject(AuthService).tieneRol('SuperAdministrador');
  readonly gimnasio = new FormControl<string | null>(null);
  readonly gimnasios = signal<Gimnasio[]>([]);
  readonly columnas = ['socio', 'dni', 'fecha', 'acciones'];
  readonly solicitudes = signal<SolicitudVinculacion[]>([]);
  readonly cargando = signal(false);
  readonly procesando = signal<string | null>(null);

  constructor() {
    if (this.esPlataforma) {
      this.api.listarGimnasios().subscribe({
        next: (l) => this.gimnasios.set(l),
        error: (e) => this.snack.open(mensajeDeError(e, 'No se pudieron cargar los gimnasios.'), 'Cerrar', { duration: 5000 }),
      });
    } else {
      this.cargar();
    }
  }

  /** Gimnasio que se envía: solo la plataforma lo indica; el Dueño usa el de su sesión. */
  private get destino(): string | null {
    return this.esPlataforma ? this.gimnasio.value : null;
  }

  cargar(): void {
    this.cargando.set(true);
    this.solicitudes.set([]);
    this.api.solicitudesPendientes(this.destino).subscribe({
      next: (s) => { this.solicitudes.set(s); this.cargando.set(false); },
      error: (e) => { this.snack.open(mensajeDeError(e), 'Cerrar', { duration: 5000 }); this.cargando.set(false); },
    });
  }

  resolver(s: SolicitudVinculacion, aprobar: boolean): void {
    this.procesando.set(s.id);
    const motivo = aprobar ? undefined
      : this.esPlataforma ? 'Rechazada por el Administrador de la Plataforma' : 'Rechazada por el gimnasio';
    this.api.resolverSolicitud(s.id, aprobar, motivo, this.destino).subscribe({
      next: () => {
        this.solicitudes.update((l) => l.filter((x) => x.id !== s.id));
        this.snack.open(aprobar ? `${s.nombreSocio} ya es socio del gimnasio` : 'Solicitud rechazada', 'Cerrar', { duration: 4000 });
        this.procesando.set(null);
      },
      error: (e) => { this.snack.open(mensajeDeError(e), 'Cerrar', { duration: 6000 }); this.procesando.set(null); },
    });
  }
}
