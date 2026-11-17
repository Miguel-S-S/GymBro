import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { BienestarHoy, RegistroBienestar, RegistroBienestarRequest } from '../models/bienestar.models';
import { API_BASE_URL } from './api.config';

/** Cliente de GymBro.Api/Controllers/BienestarController (Etapa 8b). */
@Injectable({ providedIn: 'root' })
export class BienestarApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(API_BASE_URL)}/bienestar`;

  /** Día local del servidor y el registro de hoy del socio autenticado (null si todavía no lo cargó). */
  hoy(): Observable<BienestarHoy> {
    return this.http.get<BienestarHoy>(`${this.url}/hoy`);
  }

  /** Alta o actualización del registro de `fecha` (yyyy-MM-dd; solo hoy o ayer). */
  guardar(fecha: string, pedido: RegistroBienestarRequest): Observable<RegistroBienestar> {
    return this.http.put<RegistroBienestar>(`${this.url}/${fecha}`, pedido);
  }

  /** Historial de un socio (desde y hasta en yyyy-MM-dd; por defecto, los últimos 30 días). */
  historial(socioId: string, desde?: string, hasta?: string): Observable<RegistroBienestar[]> {
    let params = new HttpParams().set('socioId', socioId);
    if (desde) params = params.set('desde', desde);
    if (hasta) params = params.set('hasta', hasta);
    return this.http.get<RegistroBienestar[]>(this.url, { params });
  }
}
