import { inject } from '@angular/core';
import { Routes } from '@angular/router';
import { authGuard, rolGuard } from './core/auth/auth.guard';
import { rutaInicial } from './core/auth/auth.models';
import { AuthService } from './core/auth/auth.service';
import { Shell } from './layout/shell';

export const routes: Routes = [
  { path: 'login', title: 'Ingresar · GymBro', loadComponent: () => import('./features/auth/login').then((m) => m.Login) },
  // Pública: el socio llega con el enlace de invitación que le entrega el gimnasio.
  {
    path: 'activar/:token',
    title: 'Activar cuenta · GymBro',
    loadComponent: () => import('./features/auth/activar-invitacion').then((m) => m.ActivarInvitacion),
  },
  // Socio: PWA con navegación inferior (Etapa 8).
  {
    path: 'mi',
    canActivate: [authGuard, rolGuard('Socio')],
    loadComponent: () => import('./features/mi/mi-layout').then((m) => m.MiLayout),
    children: [
      { path: '', title: 'Hoy · GymBro', loadComponent: () => import('./features/mi/mi-hoy').then((m) => m.MiHoy) },
      { path: 'rutina', title: 'Mi rutina · GymBro', loadComponent: () => import('./features/mi/mi-rutina').then((m) => m.MiRutina) },
      { path: 'entrenar', title: 'Entrenar · GymBro', loadComponent: () => import('./features/mi/mi-entrenar').then((m) => m.MiEntrenar) },
      { path: 'historial', title: 'Historial · GymBro', loadComponent: () => import('./features/mi/mi-historial').then((m) => m.MiHistorial) },
      {
        path: 'historial/:id',
        title: 'Sesión · GymBro',
        loadComponent: () => import('./features/mi/mi-historial').then((m) => m.MiSesionDetalle),
      },
      {
        // Etapa 8b: registro de bienestar del día (la tarjeta "¿Cómo estás hoy?" está en Hoy).
        path: 'bienestar',
        title: 'Bienestar · GymBro',
        loadComponent: () => import('./features/mi/mi-bienestar').then((m) => m.MiBienestar),
      },
      {
        path: 'mediciones',
        title: 'Mis mediciones · GymBro',
        loadComponent: () => import('./features/mi/mi-mediciones').then((m) => m.MiMediciones),
      },
      {
        path: 'gimnasio',
        title: 'Mi gimnasio · GymBro',
        loadComponent: () => import('./features/socio-portal/vinculacion').then((m) => m.Vinculacion),
      },
    ],
  },
  {
    path: '',
    component: Shell,
    canActivate: [authGuard],
    children: [
      // Cada rol entra a su pantalla de inicio (el Socio, a /mi).
      {
        path: '',
        pathMatch: 'full',
        redirectTo: () => {
          const rol = inject(AuthService).rol();
          return rol ? rutaInicial(rol) : '/login';
        },
      },

      // Dueño de Gimnasio y Personal Trainer (cada uno ve solo lo que le corresponde; lo aplica la API).
      // El Socio usa su propia área /mi (Etapa 8c, T2): estas pantallas lo redirigen a /mi.
      {
        path: 'socios',
        title: 'Socios · GymBro',
        canActivate: [rolGuard('AdministradorGimnasio', 'Entrenador')],
        loadComponent: () => import('./features/socios/socios-lista').then((m) => m.SociosLista),
      },
      {
        path: 'socios/:id',
        title: 'Socio · GymBro',
        canActivate: [rolGuard('AdministradorGimnasio', 'Entrenador')],
        loadComponent: () => import('./features/socios/socio-detalle').then((m) => m.SocioDetalle),
      },
      {
        path: 'socios/:id/sesiones/nueva',
        title: 'Registrar sesión · GymBro',
        canActivate: [rolGuard('AdministradorGimnasio', 'Entrenador')],
        loadComponent: () => import('./features/sesiones/sesion-registro').then((m) => m.SesionRegistro),
      },
      {
        path: 'sesiones/:id',
        title: 'Sesión · GymBro',
        canActivate: [rolGuard('AdministradorGimnasio', 'Entrenador')],
        loadComponent: () => import('./features/sesiones/sesion-detalle').then((m) => m.SesionDetalle),
      },
      {
        path: 'ejercicios',
        title: 'Ejercicios · GymBro',
        // Etapa 8c, T8: el catálogo no tiene datos de salud, así que también lo ve el Administrador de la Plataforma.
        canActivate: [rolGuard('AdministradorGimnasio', 'Entrenador', 'SuperAdministrador')],
        loadComponent: () => import('./features/ejercicios/ejercicios-catalogo').then((m) => m.EjerciciosCatalogo),
      },

      // Dueño de Gimnasio
      {
        path: 'gimnasio/vinculaciones',
        title: 'Vinculaciones · GymBro',
        // Etapa 9a-2c (D1): también el Administrador de la Plataforma, eligiendo el gimnasio.
        canActivate: [rolGuard('AdministradorGimnasio', 'SuperAdministrador')],
        loadComponent: () => import('./features/gimnasio/vinculaciones').then((m) => m.Vinculaciones),
      },
      {
        path: 'gimnasio/configuracion',
        title: 'Mi gimnasio · GymBro',
        canActivate: [rolGuard('AdministradorGimnasio', 'SuperAdministrador')],
        loadComponent: () => import('./features/gimnasio/mi-gimnasio').then((m) => m.MiGimnasio),
      },

      // Etapa 9a-2: equipamiento del gimnasio. Edita el Dueño; el Personal Trainer y el Administrador de la Plataforma
      // lo ven en solo lectura (regla del autor: el Administrador puede abrir todas las pantallas nuevas).
      {
        path: 'gimnasio/equipamiento',
        title: 'Equipamiento · GymBro',
        canActivate: [rolGuard('AdministradorGimnasio', 'Entrenador', 'SuperAdministrador')],
        loadComponent: () => import('./features/gimnasio/equipamiento').then((m) => m.Equipamiento),
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

      // Socio: la vinculación pasó al área /mi (se conserva la ruta anterior como redirección).
      { path: 'mi-gimnasio/vinculacion', redirectTo: '/mi/gimnasio' },
    ],
  },
  { path: '**', redirectTo: '' },
];