import { Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { ViolacionLimite } from '../../core/models/alertas.models';

/**
 * Etapa 10a (R-LIM): panel "Advertencias de límites" de la revisión. Solo aparece si el validador devolvió violaciones.
 * Advierte; no bloquea la aprobación (decisión D3). Las violaciones quedan registradas en la revisión.
 */
@Component({
  selector: 'app-advertencias-limites',
  imports: [MatIconModule],
  template: `
    @if (violaciones().length > 0) {
      <section class="advertencias-limites" role="note">
        <h3><mat-icon>warning_amber</mat-icon> Advertencias de límites</h3>
        <ul>
          @for (v of violaciones(); track $index) { <li>{{ v.mensaje }}</li> }
        </ul>
        <p class="nota">Podés aprobar igual: las advertencias quedan registradas en la revisión.</p>
      </section>
    }
  `,
  styles: `
    .advertencias-limites { margin: 8px 0; padding: 12px; border-radius: 12px;
      background: var(--mat-sys-tertiary-container); color: var(--mat-sys-on-tertiary-container); }
    h3 { display: flex; align-items: center; gap: 8px; margin: 0 0 4px; font: var(--mat-sys-title-small); }
    ul { margin: 0; padding-left: 20px; }
    .nota { margin: 4px 0 0; font: var(--mat-sys-body-small); }
  `,
})
export class AdvertenciasLimites {
  readonly violaciones = input<ViolacionLimite[]>([]);
}
