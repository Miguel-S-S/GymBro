import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AsistenciaDelDia, Cuota, GenerarCuotas, GenerarCuotasResultado, QrAsistencia, RegistroAsistencia,
} from '../models/asistencia.models';
import { API_BASE_URL } from './api.config';

/** Cliente de GymBro.Api/Controllers/AsistenciaController.cs (AsistenciaController y CuotasController), Etapa 11. */
@Injectable({ providedIn: 'root' })
export class AsistenciaApi {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);

  /** Socio: token de corta duración para dibujar su QR. */
  generarQr(): Observable<QrAsistencia> {
    return this.http.post<QrAsistencia>(`${this.base}/asistencia/qr`, null);
  }

  /** Personal: valida el código (leído o tipeado) y registra la asistencia del día. */
  registrarPorQr(token: string): Observable<RegistroAsistencia> {
    return this.http.post<RegistroAsistencia>(`${this.base}/asistencia/qr/validar`, { token });
  }

  /** Personal: registra la asistencia de un socio sin QR. */
  registrarManual(socioId: string): Observable<RegistroAsistencia> {
    return this.http.post<RegistroAsistencia>(`${this.base}/asistencia/manual`, { socioId });
  }

  /** Asistencias del día (por defecto hoy). La plataforma puede omitir el gimnasio (todos). */
  delDia(fecha: string | null = null, gimnasioId: string | null = null): Observable<AsistenciaDelDia> {
    let params = new HttpParams();
    if (fecha) params = params.set('fecha', fecha);
    if (gimnasioId) params = params.set('gimnasioId', gimnasioId);
    return this.http.get<AsistenciaDelDia>(`${this.base}/asistencia`, { params });
  }

  /** Dueño: genera las cuotas del período para los socios activos (idempotente). */
  generarCuotas(pedido: GenerarCuotas): Observable<GenerarCuotasResultado> {
    return this.http.post<GenerarCuotasResultado>(`${this.base}/cuotas/generar`, pedido);
  }

  /** Dueño: registra el pago de una cuota (sin fecha, hoy). */
  registrarPago(id: string, fechaPago: string | null = null): Observable<Cuota> {
    return this.http.put<Cuota>(`${this.base}/cuotas/${id}/pago`, { fechaPago });
  }

  cuotasDelPeriodo(anio: number, mes: number, gimnasioId: string | null = null): Observable<Cuota[]> {
    let params = new HttpParams().set('anio', anio).set('mes', mes);
    if (gimnasioId) params = params.set('gimnasioId', gimnasioId);
    return this.http.get<Cuota[]>(`${this.base}/cuotas`, { params });
  }

  morosos(gimnasioId: string | null = null): Observable<Cuota[]> {
    let params = new HttpParams();
    if (gimnasioId) params = params.set('gimnasioId', gimnasioId);
    return this.http.get<Cuota[]>(`${this.base}/cuotas/morosos`, { params });
  }

  /** Socio: sus cuotas. */
  misCuotas(): Observable<Cuota[]> {
    return this.http.get<Cuota[]>(`${this.base}/cuotas/mias`);
  }
}
