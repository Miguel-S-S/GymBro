import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Medicion, MedicionCreate } from '../models/medicion.models';
import { API_BASE_URL } from './api.config';

/** Cliente de GymBro.Api/Controllers/MedicionController. */
@Injectable({ providedIn: 'root' })
export class MedicionesApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(API_BASE_URL)}/Medicion`;

  /** Historial ordenado de la más reciente a la más antigua. */
  listarPorSocio(socioId: string): Observable<Medicion[]> {
    return this.http.get<Medicion[]>(this.url, { params: new HttpParams().set('socioId', socioId) });
  }

  registrar(dto: MedicionCreate): Observable<{ mensaje: string }> {
    return this.http.post<{ mensaje: string }>(this.url, dto);
  }
}
