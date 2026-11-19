import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  EquipamientoGimnasio,
  EquipamientoItem,
  EquipamientoMarcado,
  EquipamientoPrevisualizacion,
} from '../models/ejercicio.models';
import { API_BASE_URL } from './api.config';

/**
 * Cliente de GymBro.Api/Controllers/EquipamientoController (Etapa 9a-2). Etapa 9a-2b: el Administrador de la
 * Plataforma también escribe, indicando el gimnasio; el Dueño lo omite (sale de su sesión).
 */
@Injectable({ providedIn: 'root' })
export class EquipamientoApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(API_BASE_URL)}/equipamiento`;

  /** Dueño y Personal Trainer: el propio. Administrador de la Plataforma: el del gimnasio indicado. */
  obtener(gimnasioId?: string | null): Observable<EquipamientoGimnasio> {
    const params = gimnasioId ? new HttpParams().set('gimnasioId', gimnasioId) : undefined;
    return this.http.get<EquipamientoGimnasio>(this.url, { params });
  }

  /** Reemplaza el conjunto marcado (Dueño o Administrador de la Plataforma). */
  guardar(items: EquipamientoMarcado[], gimnasioId?: string | null): Observable<EquipamientoGimnasio> {
    return this.http.put<EquipamientoGimnasio>(this.url, { gimnasioId: gimnasioId ?? null, items });
  }

  /** Etapa 9a-2b (O1): ejercicios propios que quedarían sin poder usarse. Solo lectura. */
  previsualizar(items: EquipamientoMarcado[], gimnasioId?: string | null): Observable<EquipamientoPrevisualizacion> {
    return this.http.post<EquipamientoPrevisualizacion>(`${this.url}/previsualizacion`, { gimnasioId: gimnasioId ?? null, items });
  }

  /** Crea un tipo propio del gimnasio. */
  crearTipo(nombre: string, gimnasioId?: string | null): Observable<EquipamientoItem> {
    return this.http.post<EquipamientoItem>(`${this.url}/tipos`, { gimnasioId: gimnasioId ?? null, nombre });
  }

  /** Etapa 9a-2b: renombra un tipo (la clave no cambia). */
  editarTipo(id: string, nombre: string): Observable<EquipamientoItem> {
    return this.http.put<EquipamientoItem>(`${this.url}/tipos/${id}`, { nombre });
  }

  /** Etapa 9a-2b: baja lógica de un tipo que ningún ejercicio usa. */
  darDeBajaTipo(id: string): Observable<void> {
    return this.http.delete<void>(`${this.url}/tipos/${id}`);
  }
}
