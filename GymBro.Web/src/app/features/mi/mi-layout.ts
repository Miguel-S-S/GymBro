import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';

interface ItemNav {
  ruta: string;
  etiqueta: string;
  icono: string;
  exacto: boolean;
}

/**
 * Área del Socio (PWA, Etapa 8): pensada para el celular, con navegación inferior de 4 ítems.
 * Las opciones secundarias (Mi gimnasio, cerrar sesión) van en el menú de la barra superior.
 */
@Component({
  selector: 'app-mi-layout',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, MatToolbarModule, MatIconModule, MatButtonModule, MatMenuModule],
  template: `
    <mat-toolbar class="barra">
      <mat-icon class="logo">fitness_center</mat-icon>
      <span>GymBro</span>
      <span class="espaciador"></span>
      <button mat-icon-button [matMenuTriggerFor]="menu" aria-label="Más opciones">
        <mat-icon>more_vert</mat-icon>
      </button>
      <mat-menu #menu="matMenu">
        <div class="usuario" mat-menu-item disabled>{{ auth.sesion()?.nombreCompleto }}</div>
        <a mat-menu-item routerLink="/mi/asistencia"><mat-icon>qr_code_2</mat-icon><span>Asistencia y cuotas</span></a>
        <a mat-menu-item routerLink="/mi/gimnasio"><mat-icon>store</mat-icon><span>Mi gimnasio</span></a>
        <button mat-menu-item (click)="auth.cerrarSesion()"><mat-icon>logout</mat-icon><span>Cerrar sesión</span></button>
      </mat-menu>
    </mat-toolbar>

    <main class="contenido-mi">
      <router-outlet />
    </main>

    <nav class="nav-inferior" aria-label="Navegación principal">
      @for (i of items; track i.ruta) {
        <a [routerLink]="i.ruta" routerLinkActive="activo" [routerLinkActiveOptions]="{ exact: i.exacto }"
           #rla="routerLinkActive" [attr.aria-current]="rla.isActive ? 'page' : null">
          <mat-icon>{{ i.icono }}</mat-icon>
          <span>{{ i.etiqueta }}</span>
        </a>
      }
    </nav>
  `,
  styles: `
    :host { display: block; min-height: 100dvh; background: var(--mat-sys-surface); }
    .barra { position: sticky; top: 0; z-index: 3; background: var(--mat-sys-surface);
      border-bottom: 1px solid var(--mat-sys-outline-variant); font: var(--mat-sys-title-medium); gap: 8px; }
    .logo { color: var(--mat-sys-primary); }
    .espaciador { flex: 1; }
    .usuario { font: var(--mat-sys-label-large); }
    .contenido-mi { padding: 16px 12px calc(88px + env(safe-area-inset-bottom)); max-width: 720px; margin: 0 auto; box-sizing: border-box; }
    .nav-inferior { position: fixed; left: 0; right: 0; bottom: 0; z-index: 3; display: grid; grid-template-columns: repeat(4, 1fr);
      background: var(--mat-sys-surface-container); border-top: 1px solid var(--mat-sys-outline-variant);
      padding-bottom: env(safe-area-inset-bottom); }
    .nav-inferior a { display: flex; flex-direction: column; align-items: center; gap: 2px; padding: 8px 4px 10px;
      color: var(--mat-sys-on-surface-variant); text-decoration: none; font: var(--mat-sys-label-medium); }
    .nav-inferior a.activo { color: var(--mat-sys-on-secondary-container); }
    .nav-inferior a.activo mat-icon { background: var(--mat-sys-secondary-container); border-radius: 16px; width: 56px; }
  `,
})
export class MiLayout {
  readonly auth = inject(AuthService);

  readonly items: ItemNav[] = [
    { ruta: '/mi', etiqueta: 'Hoy', icono: 'today', exacto: true },
    { ruta: '/mi/rutina', etiqueta: 'Rutina', icono: 'list_alt', exacto: false },
    { ruta: '/mi/historial', etiqueta: 'Historial', icono: 'history', exacto: false },
    { ruta: '/mi/mediciones', etiqueta: 'Mediciones', icono: 'monitor_weight', exacto: false },
  ];
}
