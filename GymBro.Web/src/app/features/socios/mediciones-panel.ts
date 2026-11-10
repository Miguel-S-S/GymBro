import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, effect, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MedicionesApi } from '../../core/api/mediciones.api';
import { Medicion } from '../../core/models/medicion.models';
import { mensajeDeError } from '../../core/util/api-error';
import { oSinDato } from '../../core/util/formato';

@Component({
  selector: 'app-mediciones-panel',
  imports: [
    DatePipe,
    DecimalPipe,
    ReactiveFormsModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressBarModule,
  ],
  template: `
    <section class="tarjeta alta">
      <h2>Registrar medición</h2>
      <p class="aviso">
        <span>
          La API registra por ahora solo peso y altura. El resto de las métricas de <code>MedicionFisica</code>
          (IMC, % de grasa, frecuencia cardíaca, presión, sueño, estrés) no se cargan desde este formulario.
          Cada medición se agrega al historial: no reemplaza a las anteriores.
        </span>
      </p>
      <form [formGroup]="form" (ngSubmit)="registrar()" class="fila-campos">
        <mat-form-field>
          <mat-label>Peso</mat-label>
          <input matInput type="number" formControlName="pesoKg" min="1" step="0.1" />
          <span matTextSuffix>kg</span>
          @if (form.controls.pesoKg.invalid) { <mat-error>Entre 1 y 400 kg.</mat-error> }
        </mat-form-field>
        <mat-form-field>
          <mat-label>Altura</mat-label>
          <input matInput type="number" formControlName="alturaCm" min="50" step="0.5" />
          <span matTextSuffix>cm</span>
          @if (form.controls.alturaCm.invalid) { <mat-error>Entre 50 y 250 cm.</mat-error> }
        </mat-form-field>
        <button mat-flat-button type="submit" class="boton" [disabled]="guardando()">
          <mat-icon>add</mat-icon> Registrar
        </button>
      </form>
    </section>

    <h2>Historial</h2>
    @if (cargando()) {
      <mat-progress-bar mode="indeterminate" />
    } @else if (error()) {
      <div class="estado error"><mat-icon>cloud_off</mat-icon><span>{{ error() }}</span></div>
    } @else if (mediciones().length === 0) {
      <div class="estado"><mat-icon>monitor_weight</mat-icon><span>Sin mediciones registradas.</span></div>
    } @else {
      <div class="tabla-contenedor">
        <table mat-table [dataSource]="mediciones()">
          <ng-container matColumnDef="fecha">
            <th mat-header-cell *matHeaderCellDef>Fecha</th>
            <td mat-cell *matCellDef="let m">{{ m.fechaAlta | date: 'dd/MM/yyyy HH:mm' }}</td>
          </ng-container>
          <ng-container matColumnDef="peso">
            <th mat-header-cell *matHeaderCellDef class="numero">Peso (kg)</th>
            <td mat-cell *matCellDef="let m" class="numero">{{ m.pesoKg | number: '1.1-1' }}</td>
          </ng-container>
          <ng-container matColumnDef="altura">
            <th mat-header-cell *matHeaderCellDef class="numero">Altura (cm)</th>
            <td mat-cell *matCellDef="let m" class="numero">{{ m.alturaCm | number: '1.0-1' }}</td>
          </ng-container>
          <ng-container matColumnDef="imc">
            <th mat-header-cell *matHeaderCellDef class="numero">IMC</th>
            <td mat-cell *matCellDef="let m" class="numero">{{ sinDato(m.imc) === null ? '—' : (m.imc | number: '1.1-1') }}</td>
          </ng-container>
          <ng-container matColumnDef="grasa">
            <th mat-header-cell *matHeaderCellDef class="numero">% grasa</th>
            <td mat-cell *matCellDef="let m" class="numero">{{ sinDato(m.porcentajeGrasa) === null ? '—' : (m.porcentajeGrasa | number: '1.1-1') }}</td>
          </ng-container>
          <ng-container matColumnDef="fc">
            <th mat-header-cell *matHeaderCellDef class="numero">FC reposo</th>
            <td mat-cell *matCellDef="let m" class="numero">{{ sinDato(m.frecuenciaCardiacaReposo) ?? '—' }}</td>
          </ng-container>
          <ng-container matColumnDef="presion">
            <th mat-header-cell *matHeaderCellDef class="numero">Presión</th>
            <td mat-cell *matCellDef="let m" class="numero">
              {{ sinDato(m.presionArterialSistolica) === null ? '—' : (m.presionArterialSistolica | number: '1.0-0') + '/' + (m.presionArterialDiastolica | number: '1.0-0') }}
            </td>
          </ng-container>
          <ng-container matColumnDef="sueno">
            <th mat-header-cell *matHeaderCellDef class="numero">Sueño (h)</th>
            <td mat-cell *matCellDef="let m" class="numero">{{ sinDato(m.horasSuenoUltimaNoche) === null ? '—' : (m.horasSuenoUltimaNoche | number: '1.0-1') }}</td>
          </ng-container>
          <ng-container matColumnDef="estres">
            <th mat-header-cell *matHeaderCellDef class="numero">Estrés</th>
            <td mat-cell *matCellDef="let m" class="numero">{{ sinDato(m.nivelEstres) ?? '—' }}</td>
          </ng-container>
          <ng-container matColumnDef="condicion">
            <th mat-header-cell *matHeaderCellDef>Condición</th>
            <td mat-cell *matCellDef="let m">{{ m.condicionMedica || '—' }}</td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="columnas"></tr>
          <tr mat-row *matRowDef="let m; columns: columnas"></tr>
        </table>
      </div>
    }
  `,
  styles: `
    :host { display: block; padding-top: 16px; }
    h2 { font: var(--mat-sys-title-medium); margin: 0 0 12px; }
    .alta { margin-bottom: 24px; }
    .boton { align-self: center; }
  `,
})
export class MedicionesPanel {
  private readonly api = inject(MedicionesApi);
  private readonly snack = inject(MatSnackBar);

  readonly socioId = input.required<string>();

  readonly columnas = ['fecha', 'peso', 'altura', 'imc', 'grasa', 'fc', 'presion', 'sueno', 'estres', 'condicion'];
  readonly mediciones = signal<Medicion[]>([]);
  readonly cargando = signal(true);
  readonly guardando = signal(false);
  readonly error = signal<string | null>(null);
  readonly sinDato = oSinDato;

  readonly form = inject(NonNullableFormBuilder).group({
    pesoKg: [null as unknown as number, [Validators.required, Validators.min(1), Validators.max(400)]],
    alturaCm: [null as unknown as number, [Validators.required, Validators.min(50), Validators.max(250)]],
  });

  constructor() {
    effect(() => this.cargar(this.socioId()));
  }

  private cargar(socioId: string): void {
    this.cargando.set(true);
    this.error.set(null);
    this.api.listarPorSocio(socioId).subscribe({
      next: (m) => {
        this.mediciones.set(m);
        this.cargando.set(false);
      },
      error: (e) => {
        this.error.set(mensajeDeError(e, 'No se pudieron cargar las mediciones.'));
        this.cargando.set(false);
      },
    });
  }

  registrar(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const { pesoKg, alturaCm } = this.form.getRawValue();
    this.guardando.set(true);
    this.api.registrar({ socioId: this.socioId(), pesoKg, alturaCm }).subscribe({
      next: () => {
        this.guardando.set(false);
        this.form.reset();
        this.snack.open('Medición registrada', 'Cerrar', { duration: 3000 });
        this.cargar(this.socioId());
      },
      error: (e) => {
        this.guardando.set(false);
        this.snack.open(mensajeDeError(e, 'No se pudo registrar la medición.'), 'Cerrar', { duration: 6000 });
      },
    });
  }
}
