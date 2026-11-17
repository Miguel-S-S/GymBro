import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { BienestarApi } from '../../core/api/bienestar.api';
import { AuthService } from '../../core/auth/auth.service';
import { NutricionCategoria } from '../../core/models/bienestar.models';
import { mensajeDeError } from '../../core/util/api-error';
import { borrarBorradorBienestar, guardarBorradorBienestar, leerBorradorBienestar } from '../../core/util/borrador-bienestar';
import {
  ajustar, alternar, ANCLAS, CATEGORIAS_NUTRICION, construirPedido, fechaDelDispositivo, FormularioBienestar,
  formularioDesdeRegistro, formularioVacio, motivoInvalido, NIVELES_ESCALA, numeroDeTexto, RANGOS,
} from './bienestar';

type Escala = 'estres' | 'fatiga';

/**
 * ¿Cómo estás hoy? (Etapa 8b): registro de bienestar del día en menos de 30 segundos.
 * Sueño y agua con botones − / +, estrés y fatiga con 7 botones, ingesta con 3; teclado solo para peso, pulso y nota.
 * Si ya existe el registro del día, se edita. Sin conexión se conserva un borrador local; enviar requiere red.
 */
@Component({
  selector: 'app-mi-bienestar',
  imports: [DatePipe, RouterLink, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, MatProgressBarModule],
  template: `
    <a mat-button routerLink="/mi"><mat-icon>arrow_back</mat-icon> Hoy</a>
    <h1 class="titulo">¿Cómo estás hoy?</h1>

    @if (cargando()) {
      <mat-progress-bar mode="indeterminate" />
    } @else {
      @if (fecha(); as f) { <p class="meta">{{ f + 'T12:00:00' | date: "EEEE d 'de' MMMM" }}{{ registroId() ? ' · editando tu registro' : '' }}</p> }
      @if (aviso()) { <div class="aviso" role="status"><mat-icon>info</mat-icon><span>{{ aviso() }}</span></div> }

      <section class="bloque">
        <h2>Horas de sueño anoche</h2>
        <div class="paso">
          <button mat-icon-button aria-label="Menos sueño" (click)="ajustarSueno(-1)"><mat-icon>remove</mat-icon></button>
          <span class="valor">{{ form().horasSueno === null ? '—' : (form().horasSueno + ' h') }}</span>
          <button mat-icon-button aria-label="Más sueño" (click)="ajustarSueno(1)"><mat-icon>add</mat-icon></button>
          @if (form().horasSueno !== null) { <button mat-button (click)="cambiar({ horasSueno: null })">Quitar</button> }
        </div>
      </section>

      @for (e of escalas; track e.campo) {
        <section class="bloque">
          <h2>{{ e.titulo }}</h2>
          <div class="escala" role="group" [attr.aria-label]="e.titulo">
            @for (n of niveles; track n) {
              <button type="button" class="nivel" [class.elegido]="form()[e.campo] === n"
                      [attr.aria-pressed]="form()[e.campo] === n" [attr.aria-label]="e.titulo + ' ' + n"
                      (click)="elegir(e.campo, n)">{{ n }}</button>
            }
          </div>
          <div class="anclas"><span>1 · {{ e.anclas.min }}</span><span>7 · {{ e.anclas.max }}</span></div>
        </section>
      }

      <section class="bloque">
        <h2>¿Cuánto comiste hoy?</h2>
        <div class="opciones" role="group" aria-label="Ingesta del día">
          @for (c of categorias; track c.valor) {
            <button type="button" class="opcion" [class.elegido]="form().nutricionCategoria === c.valor"
                    [attr.aria-pressed]="form().nutricionCategoria === c.valor" (click)="elegirIngesta(c.valor)">{{ c.etiqueta }}</button>
          }
        </div>
      </section>

      <details class="bloque" [open]="tieneOpcionales()">
        <summary>Más datos (opcional): peso, agua, pulso y nota</summary>
        <div class="paso">
          <span class="rotulo">Agua</span>
          <button mat-icon-button aria-label="Menos agua" (click)="ajustarAgua(-1)"><mat-icon>remove</mat-icon></button>
          <span class="valor">{{ form().ingestaAguaLitros === null ? '—' : (form().ingestaAguaLitros + ' L') }}</span>
          <button mat-icon-button aria-label="Más agua" (click)="ajustarAgua(1)"><mat-icon>add</mat-icon></button>
          @if (form().ingestaAguaLitros !== null) { <button mat-button (click)="cambiar({ ingestaAguaLitros: null })">Quitar</button> }
        </div>
        <mat-form-field appearance="outline">
          <mat-label>Peso (kg)</mat-label>
          <input matInput type="number" inputmode="decimal" [min]="rangos.peso.min" [max]="rangos.peso.max" step="0.1"
                 [value]="form().pesoKg ?? ''" (input)="numero('pesoKg', $event)" />
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Pulso en reposo (latidos por minuto)</mat-label>
          <input matInput type="number" inputmode="numeric" [min]="rangos.fc.min" [max]="rangos.fc.max" step="1"
                 [value]="form().frecuenciaCardiacaReposo ?? ''" (input)="numero('frecuenciaCardiacaReposo', $event)" />
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Nota sobre la comida (opcional)</mat-label>
          <textarea matInput rows="2" [attr.maxlength]="rangos.notas" [value]="form().nutricionNotas"
                    (input)="cambiar({ nutricionNotas: texto($event) })"></textarea>
        </mat-form-field>
      </details>

      @if (motivo(); as m) { <p class="motivo">{{ m }}</p> }
      <button mat-flat-button class="principal" [disabled]="!!motivo() || guardando()" (click)="guardar()">
        <mat-icon>check</mat-icon> Guardar
      </button>
    }
  `,
  styles: `
    .titulo { font: var(--mat-sys-headline-small); margin: 4px 0 4px; }
    .meta { margin: 0 0 12px; color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-medium); }
    .bloque { margin: 0 0 16px; }
    .bloque h2 { margin: 0 0 8px; font: var(--mat-sys-title-medium); }
    details.bloque { display: flex; flex-direction: column; gap: 8px; }
    details summary { cursor: pointer; font: var(--mat-sys-title-small); margin-bottom: 8px; }
    details mat-form-field { width: 100%; }
    .paso { display: flex; align-items: center; gap: 8px; }
    .valor { min-width: 64px; text-align: center; font: var(--mat-sys-title-large); }
    .rotulo { min-width: 48px; }
    .escala { display: grid; grid-template-columns: repeat(7, 1fr); gap: 6px; }
    .nivel, .opcion { min-height: 48px; border-radius: 12px; border: 1px solid var(--mat-sys-outline);
      background: var(--mat-sys-surface); color: var(--mat-sys-on-surface); font: var(--mat-sys-title-medium); }
    .nivel.elegido, .opcion.elegido { background: var(--mat-sys-primary); color: var(--mat-sys-on-primary); border-color: var(--mat-sys-primary); }
    .anclas { display: flex; justify-content: space-between; margin-top: 4px; color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); }
    .opciones { display: grid; grid-template-columns: repeat(3, 1fr); gap: 6px; }
    .opcion { font: var(--mat-sys-label-large); padding: 4px; }
    .aviso { display: flex; gap: 8px; align-items: flex-start; margin-bottom: 12px; }
    .motivo { color: var(--mat-sys-error); margin: 0 0 8px; }
    .principal { width: 100%; height: 48px; }
  `,
})
export class MiBienestar {
  private readonly api = inject(BienestarApi);
  private readonly router = inject(Router);
  private readonly snack = inject(MatSnackBar);
  private readonly socioId = inject(AuthService).sesion()?.socioId ?? null;

  readonly rangos = RANGOS;
  readonly niveles = NIVELES_ESCALA;
  readonly categorias = CATEGORIAS_NUTRICION;
  readonly escalas = [
    { campo: 'estres' as Escala, titulo: 'Estrés', anclas: ANCLAS.estres },
    { campo: 'fatiga' as Escala, titulo: 'Fatiga', anclas: ANCLAS.fatiga },
  ];

  readonly form = signal<FormularioBienestar>(formularioVacio());
  readonly fecha = signal<string | null>(null);
  readonly registroId = signal<string | null>(null);
  readonly cargando = signal(true);
  readonly guardando = signal(false);
  readonly aviso = signal<string | null>(null);
  readonly motivo = computed(() => motivoInvalido(this.form()));
  readonly tieneOpcionales = computed(() => {
    const f = this.form();
    return f.pesoKg !== null || f.ingestaAguaLitros !== null || f.frecuenciaCardiacaReposo !== null || f.nutricionNotas !== '';
  });

  constructor() {
    if (!this.socioId) {
      this.aviso.set('Tu cuenta no tiene una ficha de socio asociada.');
      this.cargando.set(false);
      return;
    }
    const socioId = this.socioId;
    this.api.hoy().subscribe({
      next: ({ fechaLocal, registro }) => {
        this.fecha.set(fechaLocal);
        this.registroId.set(registro?.id ?? null);
        // Un borrador del mismo día tiene cambios sin enviar: prevalece sobre lo guardado.
        const borrador = leerBorradorBienestar<FormularioBienestar>(socioId, fechaLocal);
        if (borrador) {
          this.form.set({ ...formularioVacio(), ...borrador.formulario });
          this.aviso.set('Recuperamos lo que cargaste y todavía no enviaste.');
        } else if (registro) {
          this.form.set(formularioDesdeRegistro(registro));
        }
        this.cargando.set(false);
      },
      error: (e) => {
        // Sin conexión (u otro error): se puede completar igual sobre el borrador; enviar requiere red.
        const borrador = leerBorradorBienestar<FormularioBienestar>(socioId);
        this.fecha.set(borrador?.fecha ?? fechaDelDispositivo());
        if (borrador) this.form.set({ ...formularioVacio(), ...borrador.formulario });
        this.aviso.set(esSinConexion(e)
          ? 'Sin conexión: lo que cargues queda guardado en este dispositivo. Para enviarlo necesitás conexión.'
          : mensajeDeError(e));
        this.cargando.set(false);
      },
    });
  }

  cambiar(parcial: Partial<FormularioBienestar>): void {
    this.form.update((f) => ({ ...f, ...parcial }));
    const fecha = this.fecha();
    if (this.socioId && fecha) guardarBorradorBienestar(this.socioId, { fecha, formulario: this.form() });
  }

  ajustarSueno(sentido: 1 | -1): void {
    this.cambiar({ horasSueno: ajustar(this.form().horasSueno, sentido * RANGOS.sueno.paso, RANGOS.sueno) });
  }

  ajustarAgua(sentido: 1 | -1): void {
    this.cambiar({ ingestaAguaLitros: ajustar(this.form().ingestaAguaLitros, sentido * RANGOS.agua.paso, RANGOS.agua) });
  }

  elegir(campo: Escala, nivel: number): void {
    const valor = alternar(this.form()[campo], nivel);
    this.cambiar(campo === 'estres' ? { estres: valor } : { fatiga: valor });
  }

  elegirIngesta(valor: NutricionCategoria): void {
    this.cambiar({ nutricionCategoria: alternar(this.form().nutricionCategoria, valor) });
  }

  numero(campo: 'pesoKg' | 'frecuenciaCardiacaReposo', evento: Event): void {
    const valor = numeroDeTexto(this.texto(evento));
    this.cambiar(campo === 'pesoKg' ? { pesoKg: valor } : { frecuenciaCardiacaReposo: valor });
  }

  texto(evento: Event): string {
    return (evento.target as HTMLInputElement | HTMLTextAreaElement).value;
  }

  guardar(): void {
    const fecha = this.fecha();
    if (!this.socioId || !fecha || this.motivo()) return;
    if (globalThis.navigator?.onLine === false) {
      this.aviso.set('Sin conexión: tu carga quedó guardada en este dispositivo. Enviala cuando tengas conexión.');
      return;
    }
    const socioId = this.socioId;
    this.guardando.set(true);
    this.api.guardar(fecha, construirPedido(this.form())).subscribe({
      next: () => {
        borrarBorradorBienestar(socioId);
        this.guardando.set(false);
        this.snack.open('Registro del día guardado.', 'Cerrar', { duration: 4000 });
        this.router.navigateByUrl('/mi');
      },
      error: (e) => {
        this.guardando.set(false);
        this.aviso.set(esSinConexion(e)
          ? 'Sin conexión: tu carga quedó guardada en este dispositivo. Enviala cuando tengas conexión.'
          : mensajeDeError(e));
      },
    });
  }
}

function esSinConexion(e: unknown): boolean {
  return e instanceof HttpErrorResponse && e.status === 0;
}
