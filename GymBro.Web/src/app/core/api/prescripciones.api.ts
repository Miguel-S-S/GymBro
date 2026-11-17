import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { RutinaPendiente } from '../models/acceso.models';
import { API_BASE_URL } from './api.config';

/** Cliente de GymBro.Api/Controllers/PrescripcionesController. */
@Injectable({ providedIn: 'root' })
export class PrescripcionesApi {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);

  /** Historial de rutinas del socio, la más reciente primero. */
  listar(socioId: string): Observable<RutinaPendiente[]> {
    return this.http.get<RutinaPendiente[]>(`${this.base}/Socio/${socioId}/prescripciones`);
  }

  /** Genera la rutina baseline determinista (4 a 6 ejercicios). */
  generarBaseline(socioId: string): Observable<RutinaPendiente> {
    return this.http.post<RutinaPendiente>(`${this.base}/Socio/${socioId}/prescripciones/baseline`, {});
  }
}
