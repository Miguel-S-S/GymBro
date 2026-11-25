import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { AsistenciaApi } from '../../core/api/asistencia.api';
import { Cuota, QrAsistencia } from '../../core/models/asistencia.models';
import { mensajeDeError } from '../../core/util/api-error';
import { codigoEnBloques, trazoQr } from '../../core/util/qr';

/**
 * Etapa 11: el Socio muestra su QR de asistencia (token firmado de 2 minutos, dibujado en el cliente) y consulta el
 * estado de sus cuotas. "Vencida" se deriva en la API (pendiente con vencimiento anterior a hoy).
 */
@Component({
  selector: 'app-mi-asistencia',
  imports: [CurrencyPipe, DatePipe, MatButtonModule, MatIconModule, MatProgressBarModule],
  template: `
    <h1 class="titulo">Asistencia y cuotas</h1>

    <section class="tarjeta qr">
      <h2>Mi código de ingreso</h2>
      @if (errorQr()) {
        <p class="aviso">{{ errorQr() }}</p>
      } @else if (trazo(); as t) {
        <svg class="codigo" [attr.viewBox]="'0 0 ' + t.ladoConMargen + ' ' + t.ladoConMargen" role="img"
             aria-label="Código QR de asistencia" shape-rendering="crispEdges">
          <rect [attr.width]="t.ladoConMargen" [attr.height]="t.ladoConMargen" fill="#fff" />
          <path [attr.d]="t.d" fill="#000" />
        </svg>
        <p class="texto-codigo">{{ bloques() }}</p>
        <p class="meta">
          @if (restante() > 0) { Vence en {{ restante() }} s. } @else { Venció. }
          Mostralo en recepción; si la cámara no lo lee, el personal puede tipear el código.
        </p>
      } @else {
        <mat-progress-bar mode="indeterminate" />
      }
      <button mat-stroked-button type="button" (click)="pedirQr()"><mat-icon>refresh</mat-icon> Generar otro código</button>
    </section>

    <section class="tarjeta">
      <h2>Mis cuotas</h2>
      @if (errorCuotas()) {
        <p class="aviso">{{ errorCuotas() }}</p>
      }
      @for (c of cuotas(); track c.id) {
        <div class="cuota">
          <span class="periodo">{{ c.mes.toString().padStart(2, '0') }}/{{ c.anio }}</span>
          <span>{{ c.monto | currency: 'ARS' : 'symbol-narrow' : '1.2-2' }}</span>
          <span class="estado" [class.pagada]="c.estado === 'Pagada'" [class.vencida]="c.vencida">
            @if (c.estado === 'Pagada') {
              Pagada el {{ c.fechaPago | date: 'dd/MM/yyyy' }}
            } @else if (c.vencida) {
              Vencida hace {{ c.diasVencida }} {{ c.diasVencida === 1 ? 'día' : 'días' }}
            } @else {
              Pendiente, vence el {{ c.vencimiento | date: 'dd/MM/yyyy' }}
            }
          </span>
        </div>
      } @empty {
        @if (!errorCuotas()) { <p class="meta">No tenés cuotas registradas.</p> }
      }
    </section>
  `,
  styles: `
    .titulo { font: var(--mat-sys-headline-small); margin: 0 0 12px; }
    .tarjeta { border: 1px solid var(--mat-sys-outline-variant); border-radius: 16px; padding: 16px; margin-bottom: 16px; }
    h2 { font: var(--mat-sys-title-medium); margin: 0 0 12px; }
    .qr { display: grid; justify-items: center; gap: 8px; text-align: center; }
    .codigo { width: min(72vw, 280px); height: auto; }
    .texto-codigo { font: var(--mat-sys-label-large); font-family: monospace; word-break: break-all; margin: 0; }
    .meta { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); margin: 0; }
    .aviso { color: var(--mat-sys-error); }
    .cuota { display: grid; grid-template-columns: 64px 1fr; gap: 2px 12px; padding: 8px 0; border-top: 1px solid var(--mat-sys-outline-variant); }
    .periodo { font: var(--mat-sys-label-large); grid-row: span 2; align-self: center; }
    .estado { font: var(--mat-sys-body-small); color: var(--mat-sys-on-surface-variant); }
    .estado.vencida { color: var(--mat-sys-error); }
    .estado.pagada { color: var(--mat-sys-primary); }
  `,
})
export class MiAsistencia {
  private readonly api = inject(AsistenciaApi);

  readonly qr = signal<QrAsistencia | null>(null);
  readonly errorQr = signal<string | null>(null);
  readonly cuotas = signal<Cuota[]>([]);
  readonly errorCuotas = signal<string | null>(null);
  private readonly ahora = signal(Date.now());

  readonly trazo = computed(() => {
    const q = this.qr();
    return q ? trazoQr(q.token) : null;
  });
  readonly bloques = computed(() => codigoEnBloques(this.qr()?.token ?? ''));
  readonly restante = computed(() => {
    const q = this.qr();
    return q ? Math.max(0, Math.round((Date.parse(q.expiraUtc) - this.ahora()) / 1000)) : 0;
  });

  constructor() {
    this.pedirQr();
    this.api.misCuotas().subscribe({
      next: (l) => this.cuotas.set(l),
      error: (e) => this.errorCuotas.set(mensajeDeError(e, 'No se pudieron cargar tus cuotas.')),
    });
    // Cuenta regresiva; al vencer se pide un código nuevo (el token dura 2 minutos).
    const reloj = setInterval(() => {
      this.ahora.set(Date.now());
      if (this.qr() && this.restante() === 0 && !this.pidiendo) this.pedirQr();
    }, 1000);
    inject(DestroyRef).onDestroy(() => clearInterval(reloj));
  }

  private pidiendo = false;

  pedirQr(): void {
    this.pidiendo = true;
    this.errorQr.set(null);
    this.api.generarQr().subscribe({
      next: (q) => {
        this.qr.set(q);
        this.ahora.set(Date.now());
        this.pidiendo = false;
      },
      error: (e) => {
        this.qr.set(null);
        this.errorQr.set(mensajeDeError(e, 'No se pudo generar el código.'));
        this.pidiendo = false;
      },
    });
  }
}
