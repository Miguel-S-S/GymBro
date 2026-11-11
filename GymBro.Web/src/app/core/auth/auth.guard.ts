import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { Rol, rutaInicial } from './auth.models';
import { AuthService } from './auth.service';

/** Exige sesión iniciada. */
export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.autenticado() ? true : inject(Router).createUrlTree(['/login']);
};

/**
 * Exige que el usuario tenga alguno de los roles indicados; si no, lo lleva a su pantalla de inicio.
 * Es una comodidad de la interfaz: la autorización real la aplica GymBro.Api en cada endpoint.
 */
export const rolGuard =
  (...roles: Rol[]): CanActivateFn =>
  () => {
    const auth = inject(AuthService);
    const router = inject(Router);
    const rol = auth.rol();
    if (!rol) return router.createUrlTree(['/login']);
    return roles.includes(rol) ? true : router.createUrlTree([rutaInicial(rol)]);
  };