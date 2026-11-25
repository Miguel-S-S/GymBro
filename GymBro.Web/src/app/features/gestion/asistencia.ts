import { DatePipe } from '@angular/common';
import { Component, DestroyRef, ElementRef, inject, signal, viewChild } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Observable } from 'rxjs';
import { AccesoApi } from '../../core/api/acceso.api';
import { AsistenciaApi } from '../../core/api/asistencia.api';
import { SociosApi } from '../../core/api/socios.api';
import { AuthService } from '../../core/auth/auth.service';
import { Gimnasio } from '../../core/models/acceso.models';
import { AsistenciaDelDia, RegistroAsistencia } from '../../core/models/asistencia.models';
import { SocioResumen } from '../../core/models/socio.models';
import { mensajeDeError } from '../../core/util/api-error';

/** Detector de códigos del navegador (no está en lib.dom de TypeScript). */
interface DetectorCodigos {
  detect(fuente: CanvasImageSource): Promise<{ rawValue: string }[]>;
}
type ConstructorDetector = new (opciones: { formats: string[] }) => DetectorCodigos;

/** Etapa 11: la cámara se ofrece solo si el navegador implementa BarcodeDetector (limitación DV-16). */
export function detectorDisponible(w: unknown = globalThis): boolean {
  return typeof (w as { BarcodeDetector?: unknown }).BarcodeDetector === 'function';
}

/**
 * Etapa 11: asistencia del día. El Dueño y el Personal Trainer registran (QR con la cámara si el navegador lo soporta,
 * código tipeado o elección manual del socio); el Administrador de la Plataforma solo lee, entre gimnasios.
 */
@Component({
  selector: 'app-asistencia',
  imports: [DatePipe, ReactiveFormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule,
    MatProgressBarModule, MatSelectModule],
  template: `
    <div class="encabezado"><h1>Asistencia del día</h1></div>

    @if (esPlataforma) {
      <mat-form-field class="selector">
        <mat-label>Gimnasio</mat-label>
        <mat-select [formControl]="gimnasioElegido" (selectionChange)="cargar()">
          <mat-option [value]="null">Todos los gimnasios</mat-option>
          @for (g of gimnasios(); track g.id) { <mat-option [value]="g.id">{{ g.nombre }}</mat-option> }
        </mat-select>
      </mat-form-field>
      <p class="ayuda"><mat-icon>visibility</mat-icon> Solo lectura: el registro lo hace el personal de cada gimnasio.</p>
    } @else {
      <section class="tarjeta registro">
        <h2>Registrar ingreso</h2>
        @if (camaraDisponible) {
          @if (escaneando()) {
            <video #video class="video" autoplay muted playsinline></video>
            <button mat-stroked-button type="button" (click)="detenerCamara()"><mat-icon>videocam_off</mat-icon> Detener cámara</button>
          } @else {
            <button mat-flat-button type="button" (click)="iniciarCamara()"><mat-icon>qr_code_scanner</mat-icon> Leer QR con la cámara</button>
          }
        } @else {
          <p class="ayuda sin-camara"><mat-icon>info</mat-icon> Este navegador no permite leer el QR con la cámara. Tipeá el código que muestra el socio o elegilo de la lista.</p>
        }

        <form class="fila" (ngSubmit)="registrarCodigo()">
          <mat-form-field class="campo">
            <mat-label>Código del socio</mat-label>
            <input matInput [formControl]="codigo" autocomplete="off" autocapitalize="characters" />
          </mat-form-field>
          <button mat-flat-button type="submit" [disabled]="!codigo.value?.trim() || registrando()">Validar código</button>
        </form>

        <form class="fila" (ngSubmit)="registrarManual()">
          <mat-form-field class="campo">
            <mat-label>Socio (registro manual)</mat-label>
            <mat-select [formControl]="socioElegido">
              @for (s of socios(); track s.id) { <mat-option [value]="s.id">{{ s.nombreCompleto }}</mat-option> }
            </mat-select>
          </mat-form-field>
          <button mat-stroked-button type="submit" [disabled]="!socioElegido.value || registrando()">Registrar</button>
        </form>
        @if (ultimo(); as u) {
          <p class="resultado" [class.repetida]="u.yaRegistrada">
            <mat-icon>{{ u.yaRegistrada ? 'info' : 'check_circle' }}</mat-icon>
            {{ u.asistencia.socioNombre }}: {{ u.yaRegistrada ? 'ya tenía registrado el ingreso de hoy' : 'ingreso registrado' }}
            ({{ u.asistencia.metodo === 'Qr' ? 'QR' : 'manual' }}).
          </p>
        }
      </section>
    }

    @if (cargando()) {
      <mat-progress-bar mode="indeterminate" />
    } @else if (error()) {
      <div class="estado error"><mat-icon>error_outline</mat-icon><span>{{ error() }}</span></div>
    } @else if (dia(); as d) {
      <p class="total">{{ d.total }} {{ d.total === 1 ? 'ingreso' : 'ingresos' }} el {{ d.fecha | date: 'dd/MM/yyyy' }}</p>
      <div class="lista">
        @for (a of d.asistencias; track a.id) {
          <div class="item">
            <span class="hora">{{ a.instante | date: 'HH:mm' }}</span>
            <span>{{ a.socioNombre }}</span>
            <span class="meta">{{ a.metodo === 'Qr' ? 'QR' : 'Manual' }}@if (esPlataforma) { · {{ a.gimnasioNombre }} }</span>
          </div>
        } @empty {
          <p class="ayuda">Todavía no hay ingresos registrados hoy.</p>
        }
      </div>
    }
  `,
  styles: `
    .selector, .campo { width: 100%; max-width: 420px; }
    .tarjeta { border: 1px solid var(--mat-sys-outline-variant); border-radius: 16px; padding: 16px; margin-bottom: 16px; display: grid; gap: 8px; }
    h2 { font: var(--mat-sys-title-medium); margin: 0; }
    .fila { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
    .video { width: 100%; max-width: 420px; border-radius: 12px; background: #000; }
    .ayuda { display: flex; align-items: center; gap: 8px; color: var(--mat-sys-on-surface-variant); }
    .resultado { display: flex; align-items: center; gap: 8px; color: var(--mat-sys-primary); }
    .resultado.repetida { color: var(--mat-sys-on-surface-variant); }
    .total { font: var(--mat-sys-title-small); }
    .item { display: grid; grid-template-columns: 56px 1fr auto; gap: 12px; padding: 8px 0; border-top: 1px solid var(--mat-sys-outline-variant); }
    .hora { font: var(--mat-sys-label-large); }
    .meta { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); }
  `,
})
export class AsistenciaDia {
  private readonly api = inject(AsistenciaApi);
  private readonly snack = inject(MatSnackBar);
  private readonly video = viewChild<ElementRef<HTMLVideoElement>>('video');

  readonly esPlataforma = inject(AuthService).tieneRol('SuperAdministrador');
  readonly camaraDisponible = detectorDisponible();
  readonly gimnasioElegido = new FormControl<string | null>(null);
  readonly codigo = new FormControl<string>('', { nonNullable: true });
  readonly socioElegido = new FormControl<string | null>(null);
  readonly gimnasios = signal<Gimnasio[]>([]);
  readonly socios = signal<SocioResumen[]>([]);
  readonly dia = signal<AsistenciaDelDia | null>(null);
  readonly ultimo = signal<RegistroAsistencia | null>(null);
  readonly cargando = signal(true);
  readonly registrando = signal(false);
  readonly escaneando = signal(false);
  readonly error = signal<string | null>(null);

  private flujo: MediaStream | null = null;
  private temporizador: ReturnType<typeof setInterval> | null = null;

  constructor() {
    if (this.esPlataforma) {
      inject(AccesoApi).listarGimnasios().subscribe((l) => this.gimnasios.set(l));
    } else {
      inject(SociosApi).listar().subscribe((l) => this.socios.set(l));
    }
    this.cargar();
    inject(DestroyRef).onDestroy(() => this.detenerCamara());
  }

  cargar(): void {
    this.cargando.set(true);
    this.error.set(null);
    this.api.delDia(null, this.esPlataforma ? this.gimnasioElegido.value : null).subscribe({
      next: (d) => { this.dia.set(d); this.cargando.set(false); },
      error: (e) => { this.error.set(mensajeDeError(e, 'No se pudo cargar la asistencia.')); this.cargando.set(false); },
    });
  }

  registrarCodigo(texto = this.codigo.value): void {
    const token = texto.trim();
    if (!token) return;
    this.registrar(this.api.registrarPorQr(token), () => this.codigo.setValue(''));
  }

  registrarManual(): void {
    const id = this.socioElegido.value;
    if (!id) return;
    this.registrar(this.api.registrarManual(id), () => this.socioElegido.setValue(null));
  }

  private registrar(pedido: Observable<RegistroAsistencia>, limpiar: () => void): void {
    this.registrando.set(true);
    pedido.subscribe({
      next: (r) => { this.ultimo.set(r); limpiar(); this.registrando.set(false); this.cargar(); },
      error: (e) => { this.snack.open(mensajeDeError(e), 'Cerrar', { duration: 5000 }); this.registrando.set(false); },
    });
  }

  async iniciarCamara(): Promise<void> {
    const Detector = (globalThis as unknown as { BarcodeDetector?: ConstructorDetector }).BarcodeDetector;
    if (!Detector || !navigator.mediaDevices?.getUserMedia) return;
    try {
      this.flujo = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'environment' } });
    } catch {
      this.snack.open('No se pudo abrir la cámara. Tipeá el código.', 'Cerrar', { duration: 5000 });
      return;
    }
    this.escaneando.set(true);
    const detector = new Detector({ formats: ['qr_code'] });
    setTimeout(() => {
      const v = this.video()?.nativeElement;
      if (!v || !this.flujo) return;
      v.srcObject = this.flujo;
      this.temporizador = setInterval(async () => {
        if (this.registrando() || v.readyState < 2) return;
        const leidos = await detector.detect(v).catch(() => []);
        if (leidos.length > 0) {
          this.detenerCamara();
          this.registrarCodigo(leidos[0].rawValue);
        }
      }, 400);
    });
  }

  detenerCamara(): void {
    if (this.temporizador) clearInterval(this.temporizador);
    this.temporizador = null;
    this.flujo?.getTracks().forEach((t) => t.stop());
    this.flujo = null;
    this.escaneando.set(false);
  }
}
