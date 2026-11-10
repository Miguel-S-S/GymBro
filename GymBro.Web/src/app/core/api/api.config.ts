import { InjectionToken } from '@angular/core';

/**
 * Base URL de GymBro.Api.
 * En desarrollo, `ng serve` reenvía `/api` a http://localhost:5069 (ver proxy.conf.json),
 * así que no hace falta habilitar CORS en el backend.
 */
export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL', {
  providedIn: 'root',
  factory: () => '/api',
});
