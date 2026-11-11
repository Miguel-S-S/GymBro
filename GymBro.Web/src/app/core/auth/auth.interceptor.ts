import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { API_BASE_URL } from '../api/api.config';
import { AuthService } from './auth.service';

/** Adjunta el JWT a las llamadas a la API y cierra la sesión si el servidor responde 401. */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const base = inject(API_BASE_URL);

  if (!req.url.startsWith(base)) return next(req);

  const token = auth.token();
  const peticion = token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;

  return next(peticion).pipe(
    catchError((err: unknown) => {
      // Un 401 en el propio login significa "credenciales inválidas", no sesión vencida.
      const esLogin = req.url.endsWith('/auth/login');
      if (err instanceof HttpErrorResponse && err.status === 401 && !esLogin && auth.sesion()) {
        auth.cerrarSesion();
      }
      return throwError(() => err);
    }),
  );
};