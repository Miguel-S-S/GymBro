import { DatePipe } from '@angular/common';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { Router, RouterLink } from '@angular/router';
import { SesionesApi } from '../../core/api/sesiones.api';
import { Sesion } from '../../core/models/sesion.models';
import { mensajeDeError } from '../../core/util/api-error';
import { duracionLegible } from '../../core/util/formato';

interface FilaSesion {
  id: string;
  inicio: string;
  duracion: string | null;
  ejercicios: number;
  series: number;
  conPrescripcion: boolean;
}

@Component({
  selector: 'app-sesiones-panel',
  imports: [DatePipe, RouterLink, MatTableModule, MatButtonModule, MatIconModule, MatProgressBarModule],
  template: `
    <div class="acciones">
      <a mat-flat-button [routerLink]="['/socios', socioId(), 'sesiones', 'nueva']">
        <mat-icon>add</mat-icon> Registrar sesión
      </a>
    </div>

    @if (cargando()) {
      <mat-progress-bar mode="indeterminate" />
    } @else if (error()) {
      <div class="estado error"><mat-icon>cloud_off</mat-icon><span>{{ error() }}</span></div>
    } @else if (filas().length === 0) {
      <div class="estado"><mat-icon>event_busy</mat-icon><span>El socio todavía no tiene sesiones registradas.</span></div>
    } @else {
      <div class="tabla-contenedor">
        <table mat-table [dataSource]="filas()">
          <ng-container matColumnDef="inicio">
            <th mat-header-cell *matHeaderCellDef>Inicio</th>
            <td mat-cell *matCellDef="let f">{{ f.inicio | date: 'EEE dd/MM/yyyy HH:mm' }}</td>
          </ng-container>
          <ng-container matColumnDef="duracion">
            <th mat-header-cell *matHeaderCellDef>Duración</th>
            <td mat-cell *matCellDef="let f">{{ f.duracion ?? '—' }}</td>
          </ng-container>
          <ng-container matColumnDef="ejercicios">
            <th mat-header-cell *matHeaderCellDef class="numero">Ejercicios</th>
            <td mat-cell *matCellDef="let f" class="numero">{{ f.ejercicios }}</td>
          </ng-container>
          <ng-container matColumnDef="series">
            <th mat-header-cell *matHeaderCellDef class="numero">Series</th>
            <td mat-cell *matCellDef="let f" class="numero">{{ f.series }}</td>
          </ng-container>
          <ng-container matColumnDef="prescripcion">
            <th mat-header-cell *matHeaderCellDef>Prescripción</th>
            <td mat-cell *matCellDef="let f">{{ f.conPrescripcion ? 'Asociada' : 'Libre' }}</td>
          </ng-container>
          <ng-container matColumnDef="ir">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let f" class="numero"><mat-icon>chevron_right</mat-icon></td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="columnas"></tr>
          <tr mat-row *matRowDef="let f; columns: columnas" class="fila-clickeable" (click)="ver(f.id)"></tr>
        </table>
      </div>
    }
  `,
  styles: `
    :host { display: block; padding-top: 16px; }
    .acciones { display: flex; justify-content: flex-end; margin-bottom: 12px; }
  `,
})
export class SesionesPanel {
  private readonly api = inject(SesionesApi);
  private readonly router = inject(Router);

  readonly socioId = input.required<string>();

  readonly columnas = ['inicio', 'duracion', 'ejercicios', 'series', 'prescripcion', 'ir'];
  readonly sesiones = signal<Sesion[]>([]);
  readonly cargando = signal(true);
  readonly error = signal<string | null>(null);

  readonly filas = computed<FilaSesion[]>(() =>
    [...this.sesiones()]
      .sort((a, b) => b.inicio.localeCompare(a.inicio))
      .map((s) => ({
        id: s.id,
        inicio: s.inicio,
        duracion: duracionLegible(s.inicio, s.fin),
        ejercicios: s.ejercicios.length,
        series: s.ejercicios.reduce((t, e) => t + e.seriesRealizadas.length, 0),
        conPrescripcion: !!s.prescripcionEntrenamientoId,
      })),
  );

  constructor() {
    effect(() => {
      const socioId = this.socioId();
      this.cargando.set(true);
      this.error.set(null);
      this.api.listarPorSocio(socioId).subscribe({
        next: (s) => {
          this.sesiones.set(s);
          this.cargando.set(false);
        },
        error: (e) => {
          this.error.set(mensajeDeError(e, 'No se pudieron cargar las sesiones.'));
          this.cargando.set(false);
        },
      });
    });
  }

  ver(id: string): void {
    this.router.navigate(['/sesiones', id]);
  }
}
