import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AuditLog,
  FiltrosAuditoria,
  paramsAuditoria,
  Entrenador,
  EntrenadorCreate,
  Gimnasio,
  GimnasioCreate,
  RevisarRutina,
  RutinaPendiente,
  SolicitudVinculacion,
} from '../models/acceso.models';
import { API_BASE_URL } from './api.config';

/** Etapa 9a-2c: parámetro gimnasioId solo si se indicó (Administrador de la Plataforma). */
function conGimnasio(gimnasioId: string | null): HttpParams {
  return gimnasioId ? new HttpParams().set('gimnasioId', gimnasioId) : new HttpParams();
}

/** Cliente de los controladores de acceso, multi-tenant y revisión de GymBro.Api. */
@Injectable({ providedIn: 'root' })
export class AccesoApi {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);

  // SuperAdministrador
  listarGimnasios(): Observable<Gimnasio[]> {
    return this.http.get<Gimnasio[]>(`${this.base}/gimnasios`);
  }
  crearGimnasio(dto: GimnasioCreate): Observable<Gimnasio> {
    return this.http.post<Gimnasio>(`${this.base}/gimnasios`, dto);
  }
  cambiarEstadoGimnasio(id: string, activo: boolean): Observable<Gimnasio> {
    return this.http.put<Gimnasio>(`${this.base}/gimnasios/${id}/estado`, { activo });
  }
  auditoria(filtros: FiltrosAuditoria = {}): Observable<AuditLog[]> {
    return this.http.get<AuditLog[]>(`${this.base}/auditoria`, { params: paramsAuditoria(filtros) });
  }

  // AdministradorGimnasio. Etapa 9a-2c: gimnasioId solo lo envía el Administrador de la Plataforma (el Dueño usa el suyo).
  miGimnasio(gimnasioId: string | null = null): Observable<Gimnasio> {
    return this.http.get<Gimnasio>(`${this.base}/mi-gimnasio`, { params: conGimnasio(gimnasioId) });
  }
  actualizarPoliticaRevision(requiere: boolean, gimnasioId: string | null = null): Observable<Gimnasio> {
    return this.http.put<Gimnasio>(`${this.base}/mi-gimnasio/politica-revision`, {
      requiereAprobacionRutinas: requiere,
      ...(gimnasioId ? { gimnasioId } : {}),
    });
  }
  listarEntrenadores(): Observable<Entrenador[]> {
    return this.http.get<Entrenador[]>(`${this.base}/personal/entrenadores`);
  }
  crearEntrenador(dto: EntrenadorCreate): Observable<Entrenador> {
    return this.http.post<Entrenador>(`${this.base}/personal/entrenadores`, dto);
  }
  asignarSocio(entrenadorId: string, socioId: string): Observable<void> {
    return this.http.post<void>(`${this.base}/personal/entrenadores/${entrenadorId}/socios`, { socioId });
  }
  solicitudesPendientes(gimnasioId: string | null = null): Observable<SolicitudVinculacion[]> {
    return this.http.get<SolicitudVinculacion[]>(`${this.base}/vinculaciones/pendientes`, { params: conGimnasio(gimnasioId) });
  }
  resolverSolicitud(id: string, aprobar: boolean, motivo?: string, gimnasioId: string | null = null): Observable<SolicitudVinculacion> {
    return this.http.post<SolicitudVinculacion>(`${this.base}/vinculaciones/${id}/resolver`, {
      aprobar, motivo, ...(gimnasioId ? { gimnasioId } : {}),
    });
  }

  // Socio
  solicitarVinculacion(codigoGimnasio: string): Observable<SolicitudVinculacion> {
    return this.http.post<SolicitudVinculacion>(`${this.base}/vinculaciones`, { codigoGimnasio });
  }
  misSolicitudes(): Observable<SolicitudVinculacion[]> {
    return this.http.get<SolicitudVinculacion[]>(`${this.base}/vinculaciones/mias`);
  }

  // Dueño y Personal Trainer
  rutinasPendientes(): Observable<RutinaPendiente[]> {
    return this.http.get<RutinaPendiente[]>(`${this.base}/revisiones/pendientes`);
  }
  revisarRutina(id: string, dto: RevisarRutina): Observable<RutinaPendiente> {
    return this.http.post<RutinaPendiente>(`${this.base}/revisiones/${id}`, dto);
  }
}