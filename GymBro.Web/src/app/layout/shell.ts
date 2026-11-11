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
import { ROL_ETIQUETA } from '../core/auth/auth.models';
import { AuthService } from '../core/auth/auth.service';
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
          @if (auth.tieneRol('AdministradorGimnasio', 'Entrenador', 'Socio')) {
            <a mat-list-item routerLink="/socios" routerLinkActive="activo" (click)="esMovil() && nav.close()">
              <mat-icon matListItemIcon>group</mat-icon>
              <span matListItemTitle>{{ auth.rol() === 'Socio' ? 'Mi perfil' : 'Socios' }}</span>
            </a>
            <a mat-list-item routerLink="/ejercicios" routerLinkActive="activo" (click)="esMovil() && nav.close()">
              <mat-icon matListItemIcon>sports_gymnastics</mat-icon>
              <span matListItemTitle>Ejercicios</span>
            </a>
          }
          @if (auth.tieneRol('AdministradorGimnasio', 'Entrenador')) {
            <a mat-list-item routerLink="/revisiones" routerLinkActive="activo" (click)="esMovil() && nav.close()">
              <mat-icon matListItemIcon>assignment_turned_in</mat-icon>
              <span matListItemTitle>Rutinas a revisar</span>
            </a>
          }
          @if (auth.tieneRol('AdministradorGimnasio')) {
            <a mat-list-item routerLink="/gimnasio/vinculaciones" routerLinkActive="activo" (click)="esMovil() && nav.close()">
              <mat-icon matListItemIcon>how_to_reg</mat-icon>
              <span matListItemTitle>Vinculaciones</span>
            </a>
            <a mat-list-item routerLink="/gimnasio/configuracion" routerLinkActive="activo" (click)="esMovil() && nav.close()">
              <mat-icon matListItemIcon>store</mat-icon>
              <span matListItemTitle>Mi gimnasio</span>
            </a>
          }
          @if (auth.tieneRol('SuperAdministrador')) {
            <a mat-list-item routerLink="/plataforma/gimnasios" routerLinkActive="activo" (click)="esMovil() && nav.close()">
              <mat-icon matListItemIcon>domain</mat-icon>
              <span matListItemTitle>Gimnasios</span>
            </a>
            <a mat-list-item routerLink="/plataforma/auditoria" routerLinkActive="activo" (click)="esMovil() && nav.close()">
              <mat-icon matListItemIcon>fact_check</mat-icon>
              <span matListItemTitle>Auditoría</span>
            </a>
          }
          @if (auth.tieneRol('Socio')) {
            <a mat-list-item routerLink="/mi-gimnasio/vinculacion" routerLinkActive="activo" (click)="esMovil() && nav.close()">
              <mat-icon matListItemIcon>store</mat-icon>
              <span matListItemTitle>Mi gimnasio</span>
            </a>
          }
        </mat-nav-list>
      </mat-sidenav>

      <mat-sidenav-content>
        <mat-toolbar class="barra">
          @if (esMovil()) {
            <button mat-icon-button aria-label="Abrir menú" (click)="nav.toggle()">
              <mat-icon>menu</mat-icon>
            </button>
          }
          <span>{{ titulo() }}</span>
          <span class="espaciador"></span>
          <span class="usuario">{{ auth.sesion()?.nombreCompleto }}</span>
          <button mat-icon-button aria-label="Cerrar sesión" matTooltip="Cerrar sesión" (click)="auth.cerrarSesion()">
            <mat-icon>logout</mat-icon>
          </button>
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
    .espaciador { flex: 1; }
    .usuario { font: var(--mat-sys-body-medium); color: var(--mat-sys-on-surface-variant); margin-right: 4px; }
    @media (max-width: 599px) { .usuario { display: none; } }
    .contenido { padding: 24px; max-width: 1200px; margin: 0 auto; box-sizing: border-box; }
    @media (max-width: 599px) { .contenido { padding: 16px 12px; } }
  `,
})
export class Shell {
  readonly auth = inject(AuthService);
  readonly titulo = () => {
    const rol = this.auth.rol();
    return rol ? ROL_ETIQUETA[rol] : 'GymBro';
  };
  readonly esMovil = toSignal(
    inject(BreakpointObserver)
      .observe([Breakpoints.XSmall, Breakpoints.Small])
      .pipe(map((r) => r.matches)),
    { initialValue: false },
  );
}