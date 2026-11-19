import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { EquipamientoGimnasio, EquipamientoItem } from '../models/ejercicio.models';
import { API_BASE_URL } from './api.config';

/** Cliente de GymBro.Api/Controllers/EquipamientoController (Etapa 9a-2). */
@Injectable({ providedIn: 'root' })
export class EquipamientoApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(API_BASE_URL)}/equipamiento`;

  /** Dueño y Personal Trainer: el propio. Administrador de la Plataforma: el del gimnasio indicado. */
  obtener(gimnasioId?: string | null): Observable<EquipamientoGimnasio> {
    const params = gimnasioId ? new HttpParams().set('gimnasioId', gimnasioId) : undefined;
    return this.http.get<EquipamientoGimnasio>(this.url, { params });
  }

  /** Reemplaza el conjunto marcado (solo el Dueño). */
  guardar(items: { tipoEquipamientoId: string; cantidad: number | null }[]): Observable<EquipamientoGimnasio> {
    return this.http.put<EquipamientoGimnasio>(this.url, { items });
  }

  /** Crea un tipo propio del gimnasio (solo el Dueño). */
  crearTipo(nombre: string): Observable<EquipamientoItem> {
    return this.http.post<EquipamientoItem>(`${this.url}/tipos`, { nombre });
  }
}
