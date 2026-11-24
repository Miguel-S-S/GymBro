import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { LimitacionesArticulares, RutinaPendiente } from '../models/acceso.models';
import { API_BASE_URL } from './api.config';

/**
 * Cliente de GymBro.Api/Controllers/PrescripcionesController y, desde la Etapa 7d,
 * de LimitacionesArticularesController (limitaciones que usa el protocolo de piernas).
 */
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

  /** Etapa 7d: genera la rutina de piernas del protocolo (determinista, hasta 6 ejercicios de la lista curada). */
  generarPiernas(socioId: string): Observable<RutinaPendiente> {
    return this.http.post<RutinaPendiente>(`${this.base}/Socio/${socioId}/prescripciones/piernas`, {});
  }

  /** Etapa 7d: limitaciones articulares del socio (rodilla y lumbar). */
  obtenerLimitaciones(socioId: string): Observable<LimitacionesArticulares> {
    return this.http.get<LimitacionesArticulares>(`${this.base}/Socio/${socioId}/limitaciones-articulares`);
  }

  /** Etapa 7d: guarda las limitaciones articulares (exige el consentimiento para datos de salud). */
  guardarLimitaciones(socioId: string, limitaciones: LimitacionesArticulares): Observable<LimitacionesArticulares> {
    return this.http.put<LimitacionesArticulares>(`${this.base}/Socio/${socioId}/limitaciones-articulares`, limitaciones);
  }
}
