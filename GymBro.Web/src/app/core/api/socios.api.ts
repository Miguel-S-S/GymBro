import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { SocioCreate, SocioDetalle, SocioResumen } from '../models/socio.models';
import { API_BASE_URL } from './api.config';

/** Cliente de GymBro.Api/Controllers/SocioController. */
@Injectable({ providedIn: 'root' })
export class SociosApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(API_BASE_URL)}/Socio`;

  listar(): Observable<SocioResumen[]> {
    return this.http.get<SocioResumen[]>(this.url);
  }

  obtener(id: string): Observable<SocioDetalle> {
    return this.http.get<SocioDetalle>(`${this.url}/${id}`);
  }

  crear(dto: SocioCreate): Observable<SocioResumen> {
    return this.http.post<SocioResumen>(this.url, dto);
  }
}
