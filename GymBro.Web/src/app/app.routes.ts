import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'socios' },
  {
    path: 'socios',
    title: 'Socios · GymBro',
    loadComponent: () => import('./features/socios/socios-lista').then((m) => m.SociosLista),
  },
  {
    path: 'socios/:id',
    title: 'Socio · GymBro',
    loadComponent: () => import('./features/socios/socio-detalle').then((m) => m.SocioDetalle),
  },
  {
    path: 'socios/:id/sesiones/nueva',
    title: 'Registrar sesión · GymBro',
    loadComponent: () => import('./features/sesiones/sesion-registro').then((m) => m.SesionRegistro),
  },
  {
    path: 'sesiones/:id',
    title: 'Sesión · GymBro',
    loadComponent: () => import('./features/sesiones/sesion-detalle').then((m) => m.SesionDetalle),
  },
  {
    path: 'ejercicios',
    title: 'Ejercicios · GymBro',
    loadComponent: () =>
      import('./features/ejercicios/ejercicios-catalogo').then((m) => m.EjerciciosCatalogo),
  },
  { path: '**', redirectTo: 'socios' },
];
