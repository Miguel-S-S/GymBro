import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ComparacionSesion, RegistrarSesionRequest, Sesion } from '../models/sesion.models';
import { API_BASE_URL } from './api.config';

/** Cliente de GymBro.Api/Controllers/SesionesController. */
@Injectable({ providedIn: 'root' })
export class SesionesApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(API_BASE_URL)}/Sesiones`;

  listarPorSocio(socioId: string): Observable<Sesion[]> {
    return this.http.get<Sesion[]>(this.url, { params: new HttpParams().set('socioId', socioId) });
  }

  obtener(id: string): Observable<Sesion> {
    return this.http.get<Sesion>(`${this.url}/${id}`);
  }

  comparacion(id: string): Observable<ComparacionSesion> {
    return this.http.get<ComparacionSesion>(`${this.url}/${id}/comparacion`);
  }

  registrar(request: RegistrarSesionRequest): Observable<Sesion> {
    return this.http.post<Sesion>(this.url, request);
  }
}
