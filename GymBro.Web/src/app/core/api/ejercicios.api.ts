import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, forkJoin, map, of, shareReplay } from 'rxjs';
import {
  EjercicioBuscado,
  EjercicioDetalle,
  EjercicioResumen,
  FiltroCatalogo,
  Paginado,
} from '../models/ejercicio.models';
import { API_BASE_URL } from './api.config';

/** Cliente de GymBro.Api/Controllers/EjerciciosController. */
@Injectable({ providedIn: 'root' })
export class EjerciciosApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(API_BASE_URL)}/Ejercicios`;

  /** Cache de detalle por id: las sesiones solo traen EjercicioId y se resuelve el nombre acá. */
  private readonly cacheDetalle = new Map<string, Observable<EjercicioDetalle>>();

  catalogo(filtro: FiltroCatalogo): Observable<Paginado<EjercicioResumen>> {
    let params = new HttpParams()
      .set('pagina', filtro.pagina)
      .set('tamanioPagina', filtro.tamanioPagina);
    if (filtro.texto?.trim()) params = params.set('texto', filtro.texto.trim());
    if (filtro.zona) params = params.set('zona', filtro.zona);
    return this.http.get<Paginado<EjercicioResumen>>(this.url, { params });
  }

  zonas(): Observable<string[]> {
    return this.http.get<string[]>(`${this.url}/zonas`);
  }

  obtener(id: string): Observable<EjercicioDetalle> {
    let detalle$ = this.cacheDetalle.get(id);
    if (!detalle$) {
      detalle$ = this.http.get<EjercicioDetalle>(`${this.url}/${id}`).pipe(shareReplay(1));
      this.cacheDetalle.set(id, detalle$);
    }
    return detalle$;
  }

  /** Mapa id → nombre para un conjunto de ejercicios (usa la cache de detalle). */
  nombres(ids: string[]): Observable<Record<string, string>> {
    const unicos = [...new Set(ids)];
    if (unicos.length === 0) return of({});
    return forkJoin(
      unicos.map((id) =>
        this.obtener(id).pipe(
          map((e) => [id, e.nombre] as const),
          catchError(() => of([id, 'Ejercicio no encontrado'] as const)),
        ),
      ),
    ).pipe(map((pares) => Object.fromEntries(pares)));
  }

  /**
   * Búsqueda semántica (embeddings + pgvector, vía GymBro.IA).
   * La API responde 404 cuando no hay coincidencias; acá se normaliza a lista vacía.
   */
  busquedaSemantica(consulta: string, cantidad = 10): Observable<EjercicioBuscado[]> {
    const params = new HttpParams().set('consulta', consulta).set('cantidad', cantidad);
    return this.http.get<EjercicioBuscado[]>(`${this.url}/busqueda-inteligente`, { params });
  }
}
