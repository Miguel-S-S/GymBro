import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatListModule } from '@angular/material/list';
import { MatPaginatorIntl, MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatTabsModule } from '@angular/material/tabs';
import { debounceTime, distinctUntilChanged, merge } from 'rxjs';
import { EjerciciosApi } from '../../core/api/ejercicios.api';
import { EjercicioBuscado, EjercicioResumen } from '../../core/models/ejercicio.models';
import { mensajeDeError } from '../../core/util/api-error';
import { EjercicioDetalleDialog } from './ejercicio-detalle-dialog';
import { paginadorEnEspaniol } from './paginador-es';

@Component({
  selector: 'app-ejercicios-catalogo',
  imports: [
    ReactiveFormsModule,
    MatTabsModule,
    MatTableModule,
    MatPaginatorModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatListModule,
    MatProgressBarModule,
  ],
  providers: [{ provide: MatPaginatorIntl, useFactory: paginadorEnEspaniol }],
  template: `
    <div class="encabezado">
      <div>
        <h1>Ejercicios</h1>
        <p class="subtitulo">Catálogo de musculación y acondicionamiento</p>
      </div>
    </div>

    <mat-tab-group mat-stretch-tabs="false" animationDuration="0ms">
      <!-- ===== Catálogo ===== -->
      <mat-tab label="Catálogo">
        <div class="pestania">
          <div class="fila-campos">
            <mat-form-field>
              <mat-label>Buscar por nombre, músculo o equipamiento</mat-label>
              <mat-icon matPrefix>search</mat-icon>
              <input matInput [formControl]="texto" />
            </mat-form-field>
            <mat-form-field>
              <mat-label>Zona objetivo</mat-label>
              <mat-select [formControl]="zona">
                <mat-option [value]="null">Todas</mat-option>
                @for (z of zonas(); track z) {
                  <mat-option [value]="z">{{ z }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
          </div>

          @if (cargando()) { <mat-progress-bar mode="indeterminate" /> }
          @if (error()) {
            <div class="estado error"><mat-icon>cloud_off</mat-icon><span>{{ error() }}</span></div>
          } @else if (!cargando() && ejercicios().length === 0) {
            <div class="estado"><mat-icon>search_off</mat-icon><span>No hay ejercicios que coincidan con el filtro.</span></div>
          } @else {
            <div class="tabla-contenedor">
              <table mat-table [dataSource]="ejercicios()">
                <ng-container matColumnDef="nombre">
                  <th mat-header-cell *matHeaderCellDef>Nombre</th>
                  <td mat-cell *matCellDef="let e">{{ e.nombre }}</td>
                </ng-container>
                <ng-container matColumnDef="zona">
                  <th mat-header-cell *matHeaderCellDef>Zona</th>
                  <td mat-cell *matCellDef="let e">{{ e.zonaObjetivo }}</td>
                </ng-container>
                <ng-container matColumnDef="musculo">
                  <th mat-header-cell *matHeaderCellDef>Músculo principal</th>
                  <td mat-cell *matCellDef="let e">{{ e.musculoPrincipal }}</td>
                </ng-container>
                <ng-container matColumnDef="equipamiento">
                  <th mat-header-cell *matHeaderCellDef>Equipamiento</th>
                  <td mat-cell *matCellDef="let e">{{ e.equipamiento }}</td>
                </ng-container>
                <ng-container matColumnDef="dificultad">
                  <th mat-header-cell *matHeaderCellDef class="numero">Dificultad</th>
                  <td mat-cell *matCellDef="let e" class="numero">{{ e.dificultad || '—' }}</td>
                </ng-container>
                <tr mat-header-row *matHeaderRowDef="columnas"></tr>
                <tr mat-row *matRowDef="let e; columns: columnas" class="fila-clickeable" (click)="verDetalle(e.id)"></tr>
              </table>
              <mat-paginator
                [length]="total()"
                [pageIndex]="pagina() - 1"
                [pageSize]="tamanio()"
                [pageSizeOptions]="[10, 20, 50, 100]"
                (page)="cambiarPagina($event)"
              />
            </div>
          }
        </div>
      </mat-tab>

      <!-- ===== Búsqueda semántica ===== -->
      <mat-tab label="Búsqueda semántica">
        <div class="pestania">
          <p class="aviso">
            <mat-icon>info</mat-icon>
            <span>
              Busca por similitud de significado (embeddings generados con Ollama y distancia coseno en pgvector).
              Requiere que GymBro.IA esté corriendo en http://127.0.0.1:8000. Los resultados son candidatos
              recuperados del catálogo, no una prescripción.
            </span>
          </p>
          <form class="fila-campos" (submit)="$event.preventDefault(); buscarSemantico()">
            <mat-form-field class="consulta">
              <mat-label>Describí lo que buscás</mat-label>
              <input matInput [formControl]="consulta" placeholder="Ej.: ejercicio de cuádriceps en máquina sin carga en la espalda" />
            </mat-form-field>
            <mat-form-field class="cantidad">
              <mat-label>Resultados</mat-label>
              <mat-select [formControl]="cantidad">
                @for (n of [5, 10, 20]; track n) { <mat-option [value]="n">{{ n }}</mat-option> }
              </mat-select>
            </mat-form-field>
            <button mat-flat-button type="submit" class="boton" [disabled]="buscando() || !consulta.value?.trim()">
              <mat-icon>travel_explore</mat-icon> Buscar
            </button>
          </form>

          @if (buscando()) { <mat-progress-bar mode="indeterminate" /> }
          @if (errorSemantico()) {
            <div class="estado error"><mat-icon>error_outline</mat-icon><span>{{ errorSemantico() }}</span></div>
          } @else if (resultados(); as r) {
            @if (r.length === 0) {
              <div class="estado"><mat-icon>search_off</mat-icon><span>No se encontraron ejercicios similares.</span></div>
            } @else {
              <mat-nav-list class="tabla-contenedor">
                @for (e of r; track e.id; let i = $index) {
                  <a mat-list-item (click)="verDetalle(e.id)">
                    <span matListItemAvatar class="rango">{{ i + 1 }}</span>
                    <span matListItemTitle>{{ e.nombre }}</span>
                    <span matListItemLine>{{ e.zonaObjetivo }} · {{ e.musculoPrincipal }} · {{ e.equipamiento }}</span>
                  </a>
                }
              </mat-nav-list>
            }
          }
        </div>
      </mat-tab>
    </mat-tab-group>
  `,
  styles: `
    .pestania { padding-top: 16px; }
    .consulta { flex: 4 1 320px !important; }
    .cantidad { flex: 0 1 150px !important; }
    .boton { align-self: center; }
    .rango {
      display: grid; place-items: center; border-radius: 50%;
      background: var(--mat-sys-primary-container); color: var(--mat-sys-on-primary-container);
      font: var(--mat-sys-label-large);
    }
  `,
})
export class EjerciciosCatalogo {
  private readonly api = inject(EjerciciosApi);
  private readonly dialog = inject(MatDialog);
  private readonly destroyRef = inject(DestroyRef);

  readonly columnas = ['nombre', 'zona', 'musculo', 'equipamiento', 'dificultad'];

  // Catálogo
  readonly texto = new FormControl('', { nonNullable: true });
  readonly zona = new FormControl<string | null>(null);
  readonly zonas = signal<string[]>([]);
  readonly ejercicios = signal<EjercicioResumen[]>([]);
  readonly total = signal(0);
  readonly pagina = signal(1);
  readonly tamanio = signal(20);
  readonly cargando = signal(false);
  readonly error = signal<string | null>(null);

  // Búsqueda semántica
  readonly consulta = new FormControl('', { nonNullable: true });
  readonly cantidad = new FormControl(10, { nonNullable: true });
  readonly resultados = signal<EjercicioBuscado[] | null>(null);
  readonly buscando = signal(false);
  readonly errorSemantico = signal<string | null>(null);

  constructor() {
    this.api.zonas().subscribe({ next: (z) => this.zonas.set(z), error: () => this.zonas.set([]) });

    merge(this.texto.valueChanges.pipe(debounceTime(300), distinctUntilChanged()), this.zona.valueChanges)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        this.pagina.set(1);
        this.cargar();
      });

    this.cargar();
  }

  cargar(): void {
    this.cargando.set(true);
    this.error.set(null);
    this.api
      .catalogo({ texto: this.texto.value, zona: this.zona.value, pagina: this.pagina(), tamanioPagina: this.tamanio() })
      .subscribe({
        next: (r) => {
          this.ejercicios.set(r.items);
          this.total.set(r.total);
          this.cargando.set(false);
        },
        error: (e) => {
          this.error.set(mensajeDeError(e, 'No se pudo cargar el catálogo.'));
          this.cargando.set(false);
        },
      });
  }

  cambiarPagina(e: PageEvent): void {
    this.pagina.set(e.pageIndex + 1);
    this.tamanio.set(e.pageSize);
    this.cargar();
  }

  buscarSemantico(): void {
    const consulta = this.consulta.value.trim();
    if (!consulta) return;
    this.buscando.set(true);
    this.errorSemantico.set(null);
    this.api.busquedaSemantica(consulta, this.cantidad.value).subscribe({
      next: (r) => {
        this.resultados.set(r);
        this.buscando.set(false);
      },
      error: (e) => {
        this.buscando.set(false);
        if (e?.status === 404) {
          // La API responde 404 cuando no hay coincidencias.
          this.resultados.set([]);
          return;
        }
        this.errorSemantico.set(
          e?.status >= 500
            ? 'La búsqueda semántica falló. Verificá que GymBro.IA (FastAPI + Ollama) esté corriendo.'
            : mensajeDeError(e, 'No se pudo realizar la búsqueda.'),
        );
      },
    });
  }

  verDetalle(id: string): void {
    this.dialog.open(EjercicioDetalleDialog, { data: id, width: '640px', maxWidth: '95vw' });
  }
}
