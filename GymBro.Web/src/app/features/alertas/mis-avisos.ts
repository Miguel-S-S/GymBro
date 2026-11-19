import { Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { AlertasApi } from '../../core/api/alertas.api';
import { AlertaSeguridad } from '../../core/models/alertas.models';

/**
 * Etapa 10a: tarjeta "Avisos" de la PWA del Socio. Muestra sus alertas pendientes en lenguaje simple y la sugerencia de
 * carga por ejercicio ("Próxima vez: 42,5 kg"). Para el socio independiente estas reglas son su única guarda (no tiene
 * revisor). Si no hay avisos, no ocupa lugar.
 */
@Component({
  selector: 'app-mis-avisos',
  imports: [MatButtonModule, MatIconModule],
  template: `
    @if (alertas().length > 0) {
      <section class="tarjeta-avisos" aria-label="Avisos">
        <h2><mat-icon>notifications</mat-icon> Avisos</h2>
        <ul>
          @for (a of alertas(); track a.id) {
            <li [class.advertencia]="a.severidad === 'Advertencia'" [attr.data-regla]="a.reglaCodigo">
              @if (a.reglaCodigo === 'R-PROG-1' && a.cargaSugeridaKg !== null) {
                <span class="texto"><strong>{{ a.ejercicio }}</strong> · Próxima vez: {{ kg(a.cargaSugeridaKg) }} kg</span>
              } @else {
                <span class="texto">{{ a.mensaje }}</span>
              }
              <button mat-button class="entendido" (click)="entendido(a)">Entendido</button>
            </li>
          }
        </ul>
        @if (sugerencias() > 0) {
          <p class="nota">Las sugerencias de carga son orientativas: no cambian tu rutina.</p>
        }
      </section>
    }
  `,
  styles: `
    .tarjeta-avisos { border: 1px solid var(--mat-sys-outline-variant); border-radius: 16px; padding: 16px; margin-bottom: 16px; }
    h2 { display: flex; align-items: center; gap: 8px; margin: 0 0 8px; font: var(--mat-sys-title-medium); }
    ul { list-style: none; margin: 0; padding: 0; display: grid; gap: 8px; }
    li { display: flex; align-items: center; justify-content: space-between; gap: 8px; flex-wrap: wrap;
      padding: 8px 12px; border-radius: 12px; background: var(--mat-sys-surface-container); }
    li.advertencia { background: var(--mat-sys-tertiary-container); color: var(--mat-sys-on-tertiary-container); }
    .texto { flex: 1 1 200px; }
    .nota { margin: 8px 0 0; color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); }
  `,
})
export class MisAvisos {
  private readonly api = inject(AlertasApi);
  readonly alertas = signal<AlertaSeguridad[]>([]);
  readonly sugerencias = computed(() => this.alertas().filter((a) => a.tipo === 'Sugerencia').length);

  constructor() {
    // Si la consulta falla, la tarjeta no se muestra: no bloquea el resto de la pantalla.
    this.api.listar(null, 'Pendiente').subscribe({ next: (l) => this.alertas.set(l), error: () => this.alertas.set([]) });
  }

  kg(valor: number): string {
    return valor.toLocaleString('es-AR', { maximumFractionDigits: 1 });
  }

  entendido(a: AlertaSeguridad): void {
    this.api.marcarVista(a.id).subscribe({
      next: () => this.alertas.update((l) => l.filter((x) => x.id !== a.id)),
      error: () => undefined,
    });
  }
}
