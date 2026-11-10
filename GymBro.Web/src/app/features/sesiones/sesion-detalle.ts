import { DatePipe, DecimalPipe, NgTemplateOutlet } from '@angular/common';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { forkJoin, map, switchMap } from 'rxjs';
import { EjerciciosApi } from '../../core/api/ejercicios.api';
import { SesionesApi } from '../../core/api/sesiones.api';
import { ComparacionSesion, Sesion } from '../../core/models/sesion.models';
import { mensajeDeError } from '../../core/util/api-error';
import { duracionLegible, oSinDato } from '../../core/util/formato';

@Component({
  selector: 'app-sesion-detalle',
  imports: [DatePipe, DecimalPipe, NgTemplateOutlet, RouterLink, MatButtonModule, MatIconModule, MatProgressBarModule, MatTableModule, MatTooltipModule],
  template: `
    @if (sesion(); as s) {
      <a mat-button [routerLink]="['/socios', s.socioId]" class="volver"><mat-icon>arrow_back</mat-icon> Volver al socio</a>
    }

    @if (cargando()) {
      <mat-progress-bar mode="indeterminate" />
    } @else if (error()) {
      <div class="estado error"><mat-icon>error_outline</mat-icon><span>{{ error() }}</span></div>
    } @else if (sesion(); as s) {
      <div class="encabezado">
        <div>
          <h1>Sesión del {{ s.inicio | date: "EEEE dd/MM/yyyy 'a las' HH:mm" }}</h1>
          <p class="subtitulo">
            {{ s.prescripcionEntrenamientoId ? 'Asociada a una prescripción' : 'Sesión libre (sin prescripción)' }}
          </p>
        </div>
      </div>

      <dl class="tarjeta resumen">
        <div><dt>Duración</dt><dd>{{ duracion() ?? '—' }}</dd></div>
        <div><dt>Ejercicios</dt><dd>{{ s.ejercicios.length }}</dd></div>
        <div><dt>Calorías</dt><dd>{{ sinDato(s.caloriasQuemadas) ?? '—' }}</dd></div>
        <div><dt>FC máx / prom.</dt><dd>{{ sinDato(s.frecuenciaCardiacaMaxima) ?? '—' }} / {{ sinDato(s.frecuenciaCardiacaPromedio) ?? '—' }}</dd></div>
      </dl>

      @for (ej of comparacion()?.ejercicios ?? []; track ej.ejercicioId + '-' + ej.orden) {
        <section class="tarjeta ejercicio">
          <div class="cabecera">
            <span class="orden">{{ ej.orden }}</span>
            <h2>{{ nombres()[ej.ejercicioId] ?? '…' }}</h2>
          </div>

          @if (ej.prescripcion; as p) {
            <p class="objetivo">
              <mat-icon>flag</mat-icon>
              Prescrito: {{ p.seriesObjetivo }} series de {{ p.repeticionesMinimas }}–{{ p.repeticionesMaximas }} reps
              @if (p.rirObjetivo !== null) { · RIR {{ p.rirObjetivo }} }
              · descanso {{ p.descansoObjetivoSegundos }} s
            </p>
            <p class="comparacion">{{ ej.comparacion.series.mensaje }}</p>
          }

          @if (ej.ejecucion; as x) {
            <div class="metricas">
              <span><strong>{{ x.cantidadSeriesEfectivas }}</strong> efectivas</span>
              <span><strong>{{ x.cantidadSeriesCalentamiento }}</strong> de calentamiento</span>
              <span><strong>{{ x.volumenEfectivoKg | number: '1.0-1' }}</strong> kg de volumen efectivo</span>
              <span><strong>{{ x.volumenTotalKg | number: '1.0-1' }}</strong> kg de volumen total</span>
            </div>

            <div class="tabla-contenedor">
              <table mat-table [dataSource]="x.detallesSeries">
                <ng-container matColumnDef="orden">
                  <th mat-header-cell *matHeaderCellDef>#</th>
                  <td mat-cell *matCellDef="let r">{{ r.ordenSerie }}</td>
                </ng-container>
                <ng-container matColumnDef="tipo">
                  <th mat-header-cell *matHeaderCellDef>Tipo</th>
                  <td mat-cell *matCellDef="let r">{{ r.esCalentamiento ? 'Calentamiento' : 'Efectiva' }}</td>
                </ng-container>
                <ng-container matColumnDef="peso">
                  <th mat-header-cell *matHeaderCellDef class="numero">Peso (kg)</th>
                  <td mat-cell *matCellDef="let r" class="numero">{{ r.pesoLevantadoKg | number: '1.0-2' }}</td>
                </ng-container>
                <ng-container matColumnDef="reps">
                  <th mat-header-cell *matHeaderCellDef class="numero">Reps</th>
                  <td mat-cell *matCellDef="let r" class="numero">
                    {{ r.repeticiones }}
                    @if (r.repeticionesComparacion; as c) { <ng-container *ngTemplateOutlet="marca; context: { $implicit: c }" /> }
                  </td>
                </ng-container>
                <ng-container matColumnDef="rir">
                  <th mat-header-cell *matHeaderCellDef class="numero">RIR</th>
                  <td mat-cell *matCellDef="let r" class="numero">
                    {{ r.rir ?? '—' }}
                    @if (r.rirComparacion; as c) { <ng-container *ngTemplateOutlet="marca; context: { $implicit: c }" /> }
                  </td>
                </ng-container>
                <ng-container matColumnDef="descanso">
                  <th mat-header-cell *matHeaderCellDef class="numero">Descanso (s)</th>
                  <td mat-cell *matCellDef="let r" class="numero">{{ r.tiempoDescansoSegundos ?? '—' }}</td>
                </ng-container>
                <ng-container matColumnDef="volumen">
                  <th mat-header-cell *matHeaderCellDef class="numero">Volumen (kg)</th>
                  <td mat-cell *matCellDef="let r" class="numero">{{ r.volumen | number: '1.0-1' }}</td>
                </ng-container>
                <tr mat-header-row *matHeaderRowDef="columnas"></tr>
                <tr mat-row *matRowDef="let r; columns: columnas" [class.calentamiento]="r.esCalentamiento"></tr>
              </table>
            </div>
          } @else {
            <p class="comparacion">Este ejercicio estaba prescrito pero no se ejecutó.</p>
          }

          @if (observaciones()[ej.ejercicioId + '-' + ej.orden]; as obs) {
            <p class="obs"><mat-icon>notes</mat-icon> {{ obs }}</p>
          }
        </section>
      }
    }

    <ng-template #marca let-c>
      <mat-icon class="marca" [class.ok]="c.cumpleObjetivo" [matTooltip]="c.mensaje">
        {{ c.cumpleObjetivo ? 'check_circle' : 'error' }}
      </mat-icon>
    </ng-template>
  `,
  styles: `
    .volver { margin: -8px 0 8px -12px; }
    h1::first-letter { text-transform: uppercase; }
    .resumen {
      display: grid; grid-template-columns: repeat(auto-fit, minmax(140px, 1fr)); gap: 12px; margin: 0 0 16px;
      dt { font: var(--mat-sys-label-medium); color: var(--mat-sys-on-surface-variant); }
      dd { margin: 2px 0 0; font: var(--mat-sys-title-medium); }
    }
    .ejercicio { margin-bottom: 16px; }
    .cabecera { display: flex; align-items: center; gap: 12px; margin-bottom: 8px; }
    .cabecera h2 { font: var(--mat-sys-title-medium); margin: 0; }
    .orden {
      display: grid; place-items: center; width: 28px; height: 28px; border-radius: 50%;
      background: var(--mat-sys-primary); color: var(--mat-sys-on-primary); font: var(--mat-sys-label-medium);
    }
    .objetivo, .obs { display: flex; align-items: center; gap: 6px; margin: 4px 0; font: var(--mat-sys-body-medium); }
    .comparacion { margin: 4px 0 8px; color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); }
    .metricas { display: flex; flex-wrap: wrap; gap: 16px; margin: 8px 0 12px; font: var(--mat-sys-body-medium); }
    tr.calentamiento td { color: var(--mat-sys-on-surface-variant); font-style: italic; }
    .marca { font-size: 16px; width: 16px; height: 16px; vertical-align: text-bottom; color: var(--mat-sys-error); }
    .marca.ok { color: var(--mat-sys-primary); }
  `,
})
export class SesionDetalle {
  private readonly sesiones = inject(SesionesApi);
  private readonly ejercicios = inject(EjerciciosApi);

  /** Parámetro de ruta :id (id de la sesión). */
  readonly id = input.required<string>();

  readonly columnas = ['orden', 'tipo', 'peso', 'reps', 'rir', 'descanso', 'volumen'];
  readonly sesion = signal<Sesion | null>(null);
  readonly comparacion = signal<ComparacionSesion | null>(null);
  readonly nombres = signal<Record<string, string>>({});
  readonly cargando = signal(true);
  readonly error = signal<string | null>(null);
  readonly sinDato = oSinDato;

  readonly duracion = computed(() => {
    const s = this.sesion();
    return s ? duracionLegible(s.inicio, s.fin) : null;
  });

  /** Observaciones indexadas por "ejercicioId-orden" (la comparación no las incluye). */
  readonly observaciones = computed<Record<string, string>>(() =>
    Object.fromEntries(
      (this.sesion()?.ejercicios ?? [])
        .filter((e) => !!e.observaciones)
        .map((e) => [`${e.ejercicioId}-${e.orden}`, e.observaciones as string]),
    ),
  );

  constructor() {
    effect(() => {
      const id = this.id();
      this.cargando.set(true);
      this.error.set(null);
      forkJoin({ sesion: this.sesiones.obtener(id), comparacion: this.sesiones.comparacion(id) })
        .pipe(
          switchMap(({ sesion, comparacion }) =>
            this.ejercicios
              .nombres(comparacion.ejercicios.map((e) => e.ejercicioId))
              .pipe(map((nombres) => ({ sesion, comparacion, nombres }))),
          ),
        )
        .subscribe({
          next: ({ sesion, comparacion, nombres }) => {
            this.sesion.set(sesion);
            this.comparacion.set({
              ...comparacion,
              ejercicios: [...comparacion.ejercicios].sort((a, b) => a.orden - b.orden),
            });
            this.nombres.set(nombres);
            this.cargando.set(false);
          },
          error: (e) => {
            this.error.set(mensajeDeError(e, 'No se pudo cargar la sesión.'));
            this.cargando.set(false);
          },
        });
    });
  }

}
