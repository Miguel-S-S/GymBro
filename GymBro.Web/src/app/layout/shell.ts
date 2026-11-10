import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { map } from 'rxjs';

@Component({
  selector: 'app-shell',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatSidenavModule,
    MatToolbarModule,
    MatListModule,
    MatIconModule,
    MatButtonModule,
    MatTooltipModule,
  ],
  template: `
    <mat-sidenav-container class="contenedor">
      <mat-sidenav
        #nav
        [mode]="esMovil() ? 'over' : 'side'"
        [opened]="!esMovil()"
        class="sidenav"
      >
        <div class="marca">
          <mat-icon>fitness_center</mat-icon>
          <span>GymBro</span>
        </div>
        <mat-nav-list>
          <a mat-list-item routerLink="/socios" routerLinkActive="activo" (click)="esMovil() && nav.close()">
            <mat-icon matListItemIcon>group</mat-icon>
            <span matListItemTitle>Socios</span>
          </a>
          <a mat-list-item routerLink="/ejercicios" routerLinkActive="activo" (click)="esMovil() && nav.close()">
            <mat-icon matListItemIcon>sports_gymnastics</mat-icon>
            <span matListItemTitle>Ejercicios</span>
          </a>
          <a
            mat-list-item
            disabled
            matTooltip="Pendiente: GymBro.Api todavía no expone endpoints de prescripciones"
            matTooltipPosition="right"
          >
            <mat-icon matListItemIcon>assignment</mat-icon>
            <span matListItemTitle>Prescripciones</span>
          </a>
        </mat-nav-list>
      </mat-sidenav>

      <mat-sidenav-content>
        <mat-toolbar class="barra">
          @if (esMovil()) {
            <button mat-icon-button aria-label="Abrir menú" (click)="nav.toggle()">
              <mat-icon>menu</mat-icon>
            </button>
          }
          <span>Panel administrativo</span>
        </mat-toolbar>
        <main class="contenido">
          <router-outlet />
        </main>
      </mat-sidenav-content>
    </mat-sidenav-container>
  `,
  styles: `
    .contenedor { height: 100%; }
    .sidenav { width: 240px; border-right: 1px solid var(--mat-sys-outline-variant); }
    .marca {
      display: flex; align-items: center; gap: 8px;
      padding: 20px 16px 12px; font: var(--mat-sys-title-large); color: var(--mat-sys-primary);
    }
    .activo { background: var(--mat-sys-secondary-container); border-radius: 24px; }
    .barra {
      position: sticky; top: 0; z-index: 2;
      background: var(--mat-sys-surface); border-bottom: 1px solid var(--mat-sys-outline-variant);
      font: var(--mat-sys-title-medium);
    }
    .contenido { padding: 24px; max-width: 1200px; margin: 0 auto; box-sizing: border-box; }
    @media (max-width: 599px) { .contenido { padding: 16px 12px; } }
  `,
})
export class Shell {
  readonly esMovil = toSignal(
    inject(BreakpointObserver)
      .observe([Breakpoints.XSmall, Breakpoints.Small])
      .pipe(map((r) => r.matches)),
    { initialValue: false },
  );
}
