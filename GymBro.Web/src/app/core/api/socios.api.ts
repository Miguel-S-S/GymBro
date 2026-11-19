import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { EstadoSocio, Invitacion, SocioAlta, SocioCreate, SocioDetalle, SocioResumen, SocioUpdate } from '../models/socio.models';
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

  /** Alta presencial (Dueño): crea la ficha y devuelve la invitación para que el socio active su cuenta. */
  crear(dto: SocioCreate): Observable<SocioAlta> {
    return this.http.post<SocioAlta>(this.url, dto);
  }

  /** Corrige la ficha de un socio que todavía no activó su cuenta. */
  actualizar(id: string, dto: SocioUpdate): Observable<SocioDetalle> {
    return this.http.put<SocioDetalle>(`${this.url}/${id}`, dto);
  }

  /** Emite una invitación nueva; la anterior deja de valer. */
  renovarInvitacion(id: string): Observable<Invitacion> {
    return this.http.post<Invitacion>(`${this.url}/${id}/invitacion`, {});
  }

  /** Etapa 9a-2b: estado administrativo (Activo o Inactivo), informativo. Solo el Dueño. */
  cambiarEstado(id: string, estado: EstadoSocio): Observable<SocioDetalle> {
    return this.http.put<SocioDetalle>(`${this.url}/${id}/estado`, { estado });
  }

  /** Ficha sin cuenta: baja lógica. Socio con cuenta: se desvincula del gimnasio. */
  baja(id: string): Observable<void> {
    return this.http.delete<void>(`${this.url}/${id}`);
  }
}
