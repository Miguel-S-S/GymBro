import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { Router } from '@angular/router';
import { SociosApi } from '../../core/api/socios.api';
import { SocioResumen } from '../../core/models/socio.models';
import { mensajeDeError } from '../../core/util/api-error';
import { SocioAltaDialog } from './socio-alta-dialog';

@Component({
  selector: 'app-socios-lista',
  imports: [
    DatePipe,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressBarModule,
  ],
  template: `
    <div class="encabezado">
      <div>
        <h1>Socios</h1>
        <p class="subtitulo">{{ socios().length }} registrados</p>
      </div>
      <button mat-flat-button (click)="abrirAlta()">
        <mat-icon>person_add</mat-icon> Nuevo socio
      </button>
    </div>

    <mat-form-field class="buscador">
      <mat-label>Buscar por nombre o email</mat-label>
      <mat-icon matPrefix>search</mat-icon>
      <input matInput [value]="filtro()" (input)="filtro.set($any($event.target).value)" />
    </mat-form-field>

    @if (cargando()) {
      <mat-progress-bar mode="indeterminate" />
    } @else if (error()) {
      <div class="estado error">
        <mat-icon>cloud_off</mat-icon>
        <span>{{ error() }}</span>
        <button mat-button (click)="cargar()">Reintentar</button>
      </div>
    } @else if (filtrados().length === 0) {
      <div class="estado">
        <mat-icon>group_off</mat-icon>
        <span>{{ filtro() ? 'Ningún socio coincide con la búsqueda.' : 'Todavía no hay socios registrados.' }}</span>
      </div>
    } @else {
      <div class="tabla-contenedor">
        <table mat-table [dataSource]="filtrados()">
          <ng-container matColumnDef="nombre">
            <th mat-header-cell *matHeaderCellDef>Nombre</th>
            <td mat-cell *matCellDef="let s">{{ s.nombreCompleto }}</td>
          </ng-container>
          <ng-container matColumnDef="email">
            <th mat-header-cell *matHeaderCellDef>Email</th>
            <td mat-cell *matCellDef="let s">{{ s.email }}</td>
          </ng-container>
          <ng-container matColumnDef="alta">
            <th mat-header-cell *matHeaderCellDef>Alta</th>
            <td mat-cell *matCellDef="let s">{{ s.fechaAlta | date: 'dd/MM/yyyy' }}</td>
          </ng-container>
          <ng-container matColumnDef="ir">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let s" class="numero"><mat-icon>chevron_right</mat-icon></td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="columnas"></tr>
          <tr mat-row *matRowDef="let s; columns: columnas" class="fila-clickeable" (click)="verDetalle(s)"></tr>
        </table>
      </div>
    }
  `,
  styles: `
    .buscador { width: 100%; max-width: 420px; }
  `,
})
export class SociosLista {
  private readonly api = inject(SociosApi);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);
  private readonly router = inject(Router);

  readonly columnas = ['nombre', 'email', 'alta', 'ir'];
  readonly socios = signal<SocioResumen[]>([]);
  readonly cargando = signal(true);
  readonly error = signal<string | null>(null);
  readonly filtro = signal('');

  readonly filtrados = computed(() => {
    const f = this.filtro().trim().toLowerCase();
    const lista = [...this.socios()].sort((a, b) => a.nombreCompleto.localeCompare(b.nombreCompleto, 'es'));
    return f
      ? lista.filter((s) => s.nombreCompleto.toLowerCase().includes(f) || s.email.toLowerCase().includes(f))
      : lista;
  });

  constructor() {
    this.cargar();
  }

  cargar(): void {
    this.cargando.set(true);
    this.error.set(null);
    this.api.listar().subscribe({
      next: (s) => {
        this.socios.set(s);
        this.cargando.set(false);
      },
      error: (e) => {
        this.error.set(mensajeDeError(e, 'No se pudieron cargar los socios.'));
        this.cargando.set(false);
      },
    });
  }

  abrirAlta(): void {
    this.dialog
      .open(SocioAltaDialog, { width: '480px', maxWidth: '95vw' })
      .afterClosed()
      .subscribe((creado?: SocioResumen) => {
        if (!creado) return;
        this.socios.update((l) => [...l, creado]);
        this.snack.open(`Socio ${creado.nombreCompleto} registrado`, 'Cerrar', { duration: 4000 });
      });
  }

  verDetalle(s: SocioResumen): void {
    this.router.navigate(['/socios', s.id]);
  }
}
