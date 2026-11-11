/** Roles canónicos (GymBro.Domain.Enums.RolUsuario). */
export type Rol = 'SuperAdministrador' | 'AdministradorGimnasio' | 'Entrenador' | 'Socio';

/** Etiquetas de interfaz (Anexo II): el nombre técnico no se muestra al usuario. */
export const ROL_ETIQUETA: Record<Rol, string> = {
  SuperAdministrador: 'Administrador de la Plataforma',
  AdministradorGimnasio: 'Dueño de Gimnasio',
  Entrenador: 'Personal Trainer',
  Socio: 'Socio',
};

/** GymBro.Application.DTOs.Acceso.LoginResponse */
export interface Sesion {
  token: string;
  expiraUtc: string;
  rol: Rol;
  nombreCompleto: string;
  gimnasioId: string | null;
  socioId: string | null;
}

export interface LoginRequest {
  email: string;
  password: string;
}

/** Pantalla de inicio de cada rol tras iniciar sesión. */
export function rutaInicial(rol: Rol): string {
  switch (rol) {
    case 'SuperAdministrador':
      return '/plataforma/gimnasios';
    case 'AdministradorGimnasio':
    case 'Entrenador':
      return '/socios';
    case 'Socio':
      return '/mi-gimnasio/vinculacion';
  }
}