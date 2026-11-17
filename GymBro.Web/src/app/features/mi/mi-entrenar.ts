import { Component, DestroyRef, computed, effect, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router, RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { PrescripcionesApi } from '../../core/api/prescripciones.api';
import { SesionesApi } from '../../core/api/sesiones.api';
import { AuthService } from '../../core/auth/auth.service';
import { mensajeDeError } from '../../core/util/api-error';
import { borrarBorrador, guardarBorrador, leerBorrador } from '../../core/util/borrador-sesion';
import { resumenEjercicio } from '../../core/util/rutina-formato';
import { formatoMmSs, progreso, segundosRestantes } from '../../core/util/temporizador';
import {
  EjercicioEnCurso, Entrenamiento, RIR_MAXIMO, RIR_MINIMO, SerieEnCurso, agregarSerie, alternarSerie, borradorValido,
  construirSesionDesdeEntrenamiento, crearEntrenamiento, editarSerie, motivoSerieInvalida, quitarSerie, rutinaVigente,
  seriesHechas, ultimasSeries,
} from './entrenamiento';

interface WakeLockSentinelMin { release(): Promise<void>; }

/**
 * Entrenar: una tarjeta por ejercicio con las series objetivo precargadas. El socio corrige peso y repeticiones y
 * toca ✔; al marcar arranca el descanso prescripto. "Finalizar" registra la sesión vinculada a la prescripción.
 * Lo cargado se guarda como borrador local para no perderlo si se recarga la página.
 */
@Component({
  selector: 'app-mi-entrenar',
  imports: [RouterLink, MatButtonModule, MatIconModule, MatProgressBarModule],
  template: `
    <h1 class="titulo">Entrenar</h1>

    @if (cargando()) {
      <mat-progress-bar mode="indeterminate" />
    } @else if (error()) {
      <div class="aviso">{{ error() }}</div>
      <a mat-button routerLink="/mi">Volver a Hoy</a>
    } @else if (estado(); as e) {
      @for (ej of e.ejercicios; track ej.prescripcionEjercicioId; let i = $index) {
        <section class="ejercicio">
          <header>
            <h2>{{ ej.orden }}. {{ ej.nombre }}</h2>
            <p class="objetivo">Objetivo: {{ objetivo(ej) }}</p>
          </header>

          <div class="fila encabezado-fila" aria-hidden="true">
            <span>Serie</span><span>Anterior</span><span>kg</span><span>Reps</span><span>RIR</span><span></span>
          </div>
          @for (s of ej.series; track $index; let j = $index) {
            <div class="fila" [class.hecha]="s.hecha">
              <span class="num">{{ j + 1 }}</span>
              <span class="anterior">{{ anterior(ej, j) }}</span>
              <input type="number" inputmode="decimal" min="0" step="0.5" [attr.aria-label]="'Peso serie ' + (j + 1)"
                     [value]="s.pesoKg ?? ''" [disabled]="s.hecha" (change)="cambiar(i, j, 'pesoKg', $event)" />
              <input type="number" inputmode="numeric" min="1" step="1" [attr.aria-label]="'Repeticiones serie ' + (j + 1)"
                     [value]="s.repeticiones ?? ''" [disabled]="s.hecha" (change)="cambiar(i, j, 'repeticiones', $event)" />
              <select [attr.aria-label]="'RIR serie ' + (j + 1)" [disabled]="s.hecha" (change)="cambiarRir(i, j, $event)">
                <option value="" [selected]="s.rir === null">–</option>
                @for (v of valoresRir; track v) { <option [value]="v" [selected]="s.rir === v">{{ v }}</option> }
              </select>
              <button type="button" class="check" [attr.aria-label]="(s.hecha ? 'Desmarcar' : 'Marcar') + ' serie ' + (j + 1)"
                      [attr.aria-pressed]="s.hecha" (click)="marcar(i, j, s)">
                <mat-icon>check</mat-icon>
              </button>
            </div>
          }
          <div class="acciones-ej">
            <button mat-button type="button" (click)="mas(i)"><mat-icon>add</mat-icon> serie</button>
            @if (ej.series.length > 1 && !ej.series[ej.series.length - 1].hecha) {
              <button mat-button type="button" (click)="menos(i, ej.series.length - 1)"><mat-icon>remove</mat-icon> serie</button>
            }
          </div>
        </section>
      }

      <div class="finalizar">
        <p class="meta">{{ hechas() }} series hechas</p>
        <button mat-flat-button class="principal" [disabled]="enviando()" (click)="finalizar()">
          <mat-icon>flag</mat-icon> Finalizar
        </button>
        <button mat-button type="button" (click)="descartar()">Descartar entrenamiento</button>
      </div>

      @if (restante() > 0) {
        <div class="descanso" role="timer" aria-live="polite">
          <div class="barra-progreso" [style.width.%]="avance() * 100"></div>
          <mat-icon>timer</mat-icon>
          <span class="tiempo">Descanso {{ reloj() }}</span>
          <button mat-button type="button" (click)="saltarDescanso()">Saltar</button>
        </div>
      }
    }
  `,
  styles: `
    .titulo { font: var(--mat-sys-headline-small); margin: 4px 0 12px; }
    .ejercicio { border: 1px solid var(--mat-sys-outline-variant); border-radius: 16px; padding: 12px; margin-bottom: 12px; }
    .ejercicio h2 { margin: 0; font: var(--mat-sys-title-medium); }
    .objetivo { margin: 2px 0 8px; color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); }
    .fila { display: grid; grid-template-columns: 32px 1fr 64px 52px 48px 44px; gap: 6px; align-items: center; padding: 4px 0; border-radius: 8px; }
    .encabezado-fila { font: var(--mat-sys-label-small); color: var(--mat-sys-on-surface-variant); }
    .num { text-align: center; font: var(--mat-sys-label-large); }
    .anterior { color: var(--mat-sys-outline); font: var(--mat-sys-body-small); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    input, select { width: 100%; box-sizing: border-box; height: 40px; border: 1px solid var(--mat-sys-outline-variant); border-radius: 8px;
      background: var(--mat-sys-surface); color: var(--mat-sys-on-surface); font: var(--mat-sys-body-large); text-align: center; padding: 0 2px; }
    .check { height: 40px; border-radius: 8px; border: 1px solid var(--mat-sys-outline-variant); background: var(--mat-sys-surface-container);
      color: var(--mat-sys-on-surface-variant); display: grid; place-items: center; cursor: pointer; }
    .hecha { background: color-mix(in srgb, var(--mat-sys-primary) 12%, transparent); }
    .hecha .check { background: var(--mat-sys-primary); color: var(--mat-sys-on-primary); border-color: var(--mat-sys-primary); }
    .acciones-ej { display: flex; gap: 4px; margin-top: 4px; }
    .finalizar { display: flex; flex-direction: column; gap: 8px; margin: 16px 0 72px; }
    .principal { width: 100%; height: 48px; }
    .meta { margin: 0; color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-medium); }
    .descanso { position: fixed; left: 8px; right: 8px; bottom: calc(76px + env(safe-area-inset-bottom)); z-index: 4; overflow: hidden;
      display: flex; align-items: center; gap: 8px; padding: 8px 12px; border-radius: 16px;
      background: var(--mat-sys-inverse-surface); color: var(--mat-sys-inverse-on-surface); max-width: 696px; margin: 0 auto; }
    .descanso button { color: var(--mat-sys-inverse-primary); margin-left: auto; }
    .tiempo { font: var(--mat-sys-title-medium); font-variant-numeric: tabular-nums; }
    .barra-progreso { position: absolute; left: 0; bottom: 0; height: 3px; background: var(--mat-sys-inverse-primary); }
  `,
})
export class MiEntrenar {
  private readonly prescripciones = inject(PrescripcionesApi);
  private readonly sesiones = inject(SesionesApi);
  private readonly router = inject(Router);
  private readonly snack = inject(MatSnackBar);
  private readonly socioId = inject(AuthService).sesion()?.socioId ?? null;

  readonly estado = signal<Entrenamiento | null>(null);
  readonly cargando = signal(true);
  readonly enviando = signal(false);
  readonly error = signal<string | null>(null);
  readonly valoresRir = Array.from({ length: RIR_MAXIMO - RIR_MINIMO + 1 }, (_, i) => RIR_MINIMO + i);

  /** Solo refresca la pantalla; el tiempo restante se calcula con la hora de fin. */
  private readonly ahora = signal(Date.now());
  readonly restante = computed(() => segundosRestantes(this.estado()?.descanso ?? null, this.ahora()));
  readonly reloj = computed(() => formatoMmSs(this.restante()));
  readonly avance = computed(() => progreso(this.estado()?.descanso ?? null, this.ahora()));
  readonly hechas = computed(() => { const e = this.estado(); return e ? seriesHechas(e) : 0; });

  private avisado = true;
  private wakeLock: WakeLockSentinelMin | null = null;

  constructor() {
    const destroyRef = inject(DestroyRef);
    const intervalo = setInterval(() => this.ahora.set(Date.now()), 500);
    destroyRef.onDestroy(() => { clearInterval(intervalo); this.wakeLock?.release().catch(() => undefined); });

    // Borrador local: se guarda en cada cambio.
    effect(() => {
      const e = this.estado();
      if (e && this.socioId) guardarBorrador(this.socioId, e);
    });

    // Fin del descanso: vibra una vez si el dispositivo lo permite.
    effect(() => {
      const r = this.restante();
      if (r > 0) this.avisado = false;
      else if (!this.avisado && this.estado()?.descanso) {
        this.avisado = true;
        try { navigator.vibrate?.([200, 100, 200]); } catch { /* sin vibración */ }
      }
    });

    this.cargar();
  }

  private cargar(): void {
    const socioId = this.socioId;
    if (!socioId) {
      this.error.set('Tu cuenta no tiene una ficha de socio asociada.');
      this.cargando.set(false);
      return;
    }
    forkJoin({ rutinas: this.prescripciones.listar(socioId), historial: this.sesiones.listarPorSocio(socioId) }).subscribe({
      next: ({ rutinas, historial }) => {
        const vigente = rutinaVigente(rutinas);
        if (!vigente) {
          this.error.set('No tenés una rutina vigente para entrenar.');
        } else {
          const borrador = leerBorrador<Entrenamiento>(socioId);
          this.estado.set(
            borradorValido(borrador, socioId, vigente.id) ? borrador : crearEntrenamiento(socioId, vigente, ultimasSeries(historial)),
          );
          this.mantenerPantallaEncendida();
        }
        this.cargando.set(false);
      },
      error: (e) => { this.error.set(mensajeDeError(e)); this.cargando.set(false); },
    });
  }

  /** Wake Lock (opcional): evita que la pantalla se apague durante el entrenamiento. */
  private mantenerPantallaEncendida(): void {
    const wl = (navigator as Navigator & { wakeLock?: { request(t: 'screen'): Promise<WakeLockSentinelMin> } }).wakeLock;
    wl?.request('screen').then((s) => (this.wakeLock = s)).catch(() => undefined);
  }

  objetivo(ej: EjercicioEnCurso): string {
    return resumenEjercicio(ej);
  }

  anterior(ej: EjercicioEnCurso, j: number): string {
    const a = ej.anterior[j];
    return a ? `${a.pesoLevantadoKg} kg × ${a.repeticiones}` : '–';
  }

  cambiar(i: number, j: number, campo: 'pesoKg' | 'repeticiones', ev: Event): void {
    const texto = (ev.target as HTMLInputElement).value;
    const valor = texto === '' ? null : Number(texto);
    this.estado.update((e) => (e ? editarSerie(e, i, j, { [campo]: valor }) : e));
  }

  cambiarRir(i: number, j: number, ev: Event): void {
    const texto = (ev.target as HTMLSelectElement).value;
    this.estado.update((e) => (e ? editarSerie(e, i, j, { rir: texto === '' ? null : Number(texto) }) : e));
  }

  marcar(i: number, j: number, s: SerieEnCurso): void {
    const motivo = s.hecha ? null : motivoSerieInvalida(s);
    if (motivo) {
      this.snack.open(motivo, 'Cerrar', { duration: 3000 });
      return;
    }
    this.estado.update((e) => (e ? alternarSerie(e, i, j, Date.now()) : e));
    this.ahora.set(Date.now());
  }

  mas(i: number): void {
    this.estado.update((e) => (e ? agregarSerie(e, i) : e));
  }

  menos(i: number, j: number): void {
    this.estado.update((e) => (e ? quitarSerie(e, i, j) : e));
  }

  saltarDescanso(): void {
    this.avisado = true;
    this.estado.update((e) => (e ? { ...e, descanso: null } : e));
  }

  finalizar(): void {
    const e = this.estado();
    if (!e || !this.socioId) return;
    let request;
    try {
      request = construirSesionDesdeEntrenamiento(e, new Date());
    } catch (err) {
      this.snack.open((err as Error).message, 'Cerrar', { duration: 4000 });
      return;
    }
    this.enviando.set(true);
    this.sesiones.registrar(request).subscribe({
      next: (sesion) => {
        borrarBorrador(this.socioId!);
        this.snack.open('Sesión registrada.', 'Cerrar', { duration: 4000 });
        this.router.navigate(['/mi/historial', sesion.id]);
      },
      // El borrador se conserva: el socio puede reintentar.
      error: (err) => { this.snack.open(mensajeDeError(err), 'Cerrar', { duration: 7000 }); this.enviando.set(false); },
    });
  }

  descartar(): void {
    if (!this.socioId || !confirm('¿Descartar el entrenamiento? Se pierde lo cargado.')) return;
    borrarBorrador(this.socioId);
    this.estado.set(null);
    this.router.navigateByUrl('/mi');
  }
}
