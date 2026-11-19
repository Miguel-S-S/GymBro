import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AlertaSeguridad } from '../models/alertas.models';
import { API_BASE_URL } from './api.config';

/** Cliente de GymBro.Api/Controllers/AlertasController (Etapa 10a). */
@Injectable({ providedIn: 'root' })
export class AlertasApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(API_BASE_URL)}/alertas`;

  /** Socio: las suyas. Personal Trainer: sus asignados. Dueño: su gimnasio. Con socioId, solo ese socio. */
  listar(socioId?: string | null, estado?: 'Pendiente' | 'Vista'): Observable<AlertaSeguridad[]> {
    let params = new HttpParams();
    if (socioId) params = params.set('socioId', socioId);
    if (estado) params = params.set('estado', estado);
    return this.http.get<AlertaSeguridad[]>(this.url, { params });
  }

  marcarVista(id: string): Observable<AlertaSeguridad> {
    return this.http.put<AlertaSeguridad>(`${this.url}/${id}/vista`, {});
  }
}
