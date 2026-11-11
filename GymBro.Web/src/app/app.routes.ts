import { Routes } from '@angular/router';
import { authGuard, rolGuard } from './core/auth/auth.guard';
import { Shell } from './layout/shell';

export const routes: Routes = [
  { path: 'login', title: 'Ingresar · GymBro', loadComponent: () => import('./features/auth/login').then((m) => m.Login) },
  {
    path: '',
    component: Shell,
    canActivate: [authGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'socios' },

      // Dueño de Gimnasio, Personal Trainer y Socio (cada uno ve solo lo que le corresponde; lo aplica la API).
      {
        path: 'socios',
        title: 'Socios · GymBro',
        canActivate: [rolGuard('AdministradorGimnasio', 'Entrenador', 'Socio')],
        loadComponent: () => import('./features/socios/socios-lista').then((m) => m.SociosLista),
      },
      {
        path: 'socios/:id',
        title: 'Socio · GymBro',
        canActivate: [rolGuard('AdministradorGimnasio', 'Entrenador', 'Socio')],
        loadComponent: () => import('./features/socios/socio-detalle').then((m) => m.SocioDetalle),
      },
      {
        path: 'socios/:id/sesiones/nueva',
        title: 'Registrar sesión · GymBro',
        canActivate: [rolGuard('AdministradorGimnasio', 'Entrenador', 'Socio')],
        loadComponent: () => import('./features/sesiones/sesion-registro').then((m) => m.SesionRegistro),
      },
      {
        path: 'sesiones/:id',
        title: 'Sesión · GymBro',
        canActivate: [rolGuard('AdministradorGimnasio', 'Entrenador', 'Socio')],
        loadComponent: () => import('./features/sesiones/sesion-detalle').then((m) => m.SesionDetalle),
      },
      {
        path: 'ejercicios',
        title: 'Ejercicios · GymBro',
        canActivate: [rolGuard('AdministradorGimnasio', 'Entrenador', 'Socio')],
        loadComponent: () => import('./features/ejercicios/ejercicios-catalogo').then((m) => m.EjerciciosCatalogo),
      },

      // Dueño de Gimnasio
      {
        path: 'gimnasio/vinculaciones',
        title: 'Vinculaciones · GymBro',
        canActivate: [rolGuard('AdministradorGimnasio')],
        loadComponent: () => import('./features/gimnasio/vinculaciones').then((m) => m.Vinculaciones),
      },
      {
        path: 'gimnasio/configuracion',
        title: 'Mi gimnasio · GymBro',
        canActivate: [rolGuard('AdministradorGimnasio')],
        loadComponent: () => import('./features/gimnasio/mi-gimnasio').then((m) => m.MiGimnasio),
      },

      // Dueño y Personal Trainer
      {
        path: 'revisiones',
        title: 'Rutinas a revisar · GymBro',
        canActivate: [rolGuard('AdministradorGimnasio', 'Entrenador')],
        loadComponent: () => import('./features/revisiones/revisiones-pendientes').then((m) => m.RevisionesPendientes),
      },

      // Administrador de la Plataforma
      {
        path: 'plataforma/gimnasios',
        title: 'Gimnasios · GymBro',
        canActivate: [rolGuard('SuperAdministrador')],
        loadComponent: () => import('./features/plataforma/gimnasios').then((m) => m.Gimnasios),
      },
      {
        path: 'plataforma/auditoria',
        title: 'Auditoría · GymBro',
        canActivate: [rolGuard('SuperAdministrador')],
        loadComponent: () => import('./features/plataforma/auditoria').then((m) => m.Auditoria),
      },

      // Socio
      {
        path: 'mi-gimnasio/vinculacion',
        title: 'Mi gimnasio · GymBro',
        canActivate: [rolGuard('Socio')],
        loadComponent: () => import('./features/socio-portal/vinculacion').then((m) => m.Vinculacion),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];