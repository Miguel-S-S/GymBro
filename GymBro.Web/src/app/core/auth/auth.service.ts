import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { API_BASE_URL } from '../api/api.config';
import { LoginRequest, Rol, Sesion, rutaInicial } from './auth.models';

const CLAVE = 'gymbro.sesion';

/** Sesión del usuario: guarda el JWT y expone el rol como signal para menús y guards. */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly url = `${inject(API_BASE_URL)}/auth`;

  private readonly _sesion = signal<Sesion | null>(this.leer());

  readonly sesion = this._sesion.asReadonly();
  readonly autenticado = computed(() => this.vigente(this._sesion()));
  readonly rol = computed<Rol | null>(() => (this.autenticado() ? this._sesion()!.rol : null));
  readonly token = computed(() => (this.autenticado() ? this._sesion()!.token : null));

  login(dto: LoginRequest): Observable<Sesion> {
    return this.http.post<Sesion>(`${this.url}/login`, dto).pipe(tap((s) => this.guardar(s)));
  }

  cerrarSesion(): void {
    this._sesion.set(null);
    try {
      localStorage.removeItem(CLAVE);
    } catch {
      /* almacenamiento no disponible */
    }
    this.router.navigateByUrl('/login');
  }

  /** Navega a la pantalla inicial del rol. */
  irAInicio(): void {
    const rol = this.rol();
    this.router.navigateByUrl(rol ? rutaInicial(rol) : '/login');
  }

  tieneRol(...roles: Rol[]): boolean {
    const r = this.rol();
    return r !== null && roles.includes(r);
  }

  private guardar(s: Sesion): void {
    this._sesion.set(s);
    try {
      localStorage.setItem(CLAVE, JSON.stringify(s));
    } catch {
      /* la sesión vive solo en memoria */
    }
  }

  private leer(): Sesion | null {
    try {
      const crudo = localStorage.getItem(CLAVE);
      const s = crudo ? (JSON.parse(crudo) as Sesion) : null;
      return s && this.vigente(s) ? s : null;
    } catch {
      return null;
    }
  }

  private vigente(s: Sesion | null): boolean {
    return !!s && new Date(s.expiraUtc).getTime() > Date.now();
  }
}