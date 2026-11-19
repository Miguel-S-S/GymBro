import { Component, inject, signal } from '@angular/core';
import { FormControl, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AccesoApi } from '../../core/api/acceso.api';
import { EquipamientoApi } from '../../core/api/equipamiento.api';
import { AuthService } from '../../core/auth/auth.service';
import { Gimnasio } from '../../core/models/acceso.models';
import {
  EquipamientoGimnasio,
  EquipamientoItem,
  EquipamientoMarcado,
  EquipamientoPrevisualizacion,
} from '../../core/models/ejercicio.models';
import { ConfirmarDatos, ConfirmarDialog } from '../../core/ui/confirmar-dialog';
import { mensajeDeError } from '../../core/util/api-error';

/** Fila editable: el tipo y su estado local (se envía todo junto con "Guardar"). */
interface Fila {
  item: EquipamientoItem;
  marcado: boolean;
  cantidad: number | null;
}

/**
 * Etapa 9a-2: equipamiento del gimnasio. El Dueño marca lo que tiene y agrega tipos propios; el Personal Trainer lo ve
 * en solo lectura. Etapa 9a-2b: el Administrador de la Plataforma elige el gimnasio y también lo edita; el Dueño
 * renombra y da de baja sus tipos propios (la plataforma, cualquiera). Antes de guardar se previsualiza cuántos
 * ejercicios propios quedan sin poder usarse (O1): se informa y se confirma, no se bloquea.
 */
@Component({
  selector: 'app-equipamiento',
  imports: [FormsModule, ReactiveFormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule,
    MatProgressBarModule, MatSelectModule, MatSlideToggleModule],
  template: `
    <div class="encabezado"><div>
      <h1>Equipamiento</h1>
      <p class="subtitulo">Lo que el gimnasio tiene disponible. La rutina baseline solo usa ejercicios factibles con este equipamiento.</p>
    </div></div>

    @if (esPlataforma) {
      <mat-form-field class="selector">
        <mat-label>Gimnasio</mat-label>
        <mat-select [formControl]="gimnasio" (selectionChange)="cargar($event.value)">
          @for (g of gimnasios(); track g.id) { <mat-option [value]="g.id">{{ g.nombre }}</mat-option> }
        </mat-select>
      </mat-form-field>
    }

    @if (cargando()) { <mat-progress-bar mode="indeterminate" /> }
    @if (error()) {
      <div class="estado error"><mat-icon>cloud_off</mat-icon><span>{{ error() }}</span></div>
    } @else if (datos(); as d) {
      @if (!d.editable) {
        <p class="aviso"><mat-icon>visibility</mat-icon><span>Solo lectura: el equipamiento lo declara el Dueño del gimnasio.</span></p>
      }
      <ul class="lista">
        @for (f of filas(); track f.item.tipoEquipamientoId) {
          <li class="fila" [attr.data-clave]="f.item.clave">
            @if (editando() === f.item.tipoEquipamientoId) {
              <form class="renombrar" (submit)="$event.preventDefault(); confirmarNombre(f)">
                <mat-form-field subscriptSizing="dynamic">
                  <mat-label>Nombre</mat-label>
                  <input matInput [formControl]="nombreEditado" maxlength="80" />
                </mat-form-field>
                <button mat-icon-button type="submit" aria-label="Guardar nombre" class="ok-nombre" [disabled]="nombreEditado.invalid"><mat-icon>check</mat-icon></button>
                <button mat-icon-button type="button" aria-label="Cancelar" (click)="editando.set(null)"><mat-icon>close</mat-icon></button>
              </form>
            } @else {
              <mat-slide-toggle [checked]="f.marcado" [disabled]="!d.editable || f.item.siempreDisponible"
                (change)="f.marcado = $event.checked">
                {{ f.item.nombre }}
                @if (f.item.propio) { <span class="insignia">Propio</span> }
              </mat-slide-toggle>
              @if (gestionaTipo(d, f.item)) {
                <button mat-icon-button class="renombrar-tipo" aria-label="Renombrar" (click)="renombrar(f)"><mat-icon>edit</mat-icon></button>
                @if (!f.item.siempreDisponible) {
                  <button mat-icon-button class="baja-tipo" aria-label="Dar de baja el tipo" (click)="darDeBajaTipo(f)"><mat-icon>delete</mat-icon></button>
                }
              }
            }
            @if (f.item.siempreDisponible) {
              <span class="ayuda">Incluido siempre</span>
            } @else if (f.marcado && editando() !== f.item.tipoEquipamientoId) {
              <mat-form-field class="cantidad" subscriptSizing="dynamic">
                <mat-label>Cantidad</mat-label>
                <input matInput type="number" min="1" [readonly]="!d.editable" [(ngModel)]="f.cantidad" />
              </mat-form-field>
            }
          </li>
        }
      </ul>

      @if (d.editable) {
        <form class="nuevo" (submit)="$event.preventDefault(); agregarTipo()">
          <mat-form-field>
            <mat-label>Agregar un tipo propio</mat-label>
            <input matInput [formControl]="nuevoTipo" maxlength="80" placeholder="Ej.: Kettlebell" />
          </mat-form-field>
          <button mat-stroked-button type="submit" [disabled]="nuevoTipo.invalid || guardando()">
            <mat-icon>add</mat-icon> Agregar
          </button>
        </form>
        <button mat-flat-button class="guardar" (click)="guardar()" [disabled]="guardando()">Guardar</button>
      }
    } @else if (esPlataforma && !cargando()) {
      <div class="estado"><mat-icon>domain</mat-icon><span>Elegí un gimnasio para ver su equipamiento.</span></div>
    }
  `,
  styles: `
    .selector { width: 100%; max-width: 420px; }
    .lista { list-style: none; padding: 0; margin: 0 0 16px; max-width: 640px; }
    .fila { display: flex; align-items: center; gap: 8px 16px; min-height: 56px; flex-wrap: wrap; }
    .cantidad { width: 120px; }
    .ayuda { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); }
    .insignia { margin-left: 8px; padding: 0 8px; border-radius: 8px; font: var(--mat-sys-label-small);
      background: var(--mat-sys-secondary-container); color: var(--mat-sys-on-secondary-container); }
    .nuevo, .renombrar { display: flex; gap: 12px; align-items: center; flex-wrap: wrap; max-width: 560px; }
    .aviso { display: flex; gap: 8px; align-items: center; color: var(--mat-sys-on-surface-variant); }
  `,
})
export class Equipamiento {
  private readonly api = inject(EquipamientoApi);
  private readonly acceso = inject(AccesoApi);
  private readonly snack = inject(MatSnackBar);
  private readonly dialog = inject(MatDialog);

  readonly esPlataforma = inject(AuthService).tieneRol('SuperAdministrador');
  readonly gimnasio = new FormControl<string | null>(null);
  readonly gimnasios = signal<Gimnasio[]>([]);
  readonly datos = signal<EquipamientoGimnasio | null>(null);
  readonly filas = signal<Fila[]>([]);
  readonly cargando = signal(false);
  readonly guardando = signal(false);
  readonly error = signal<string | null>(null);
  readonly nuevoTipo = new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(80)] });
  readonly editando = signal<string | null>(null);
  readonly nombreEditado = new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(80)] });

  constructor() {
    if (this.esPlataforma) {
      this.acceso.listarGimnasios().subscribe({
        next: (l) => this.gimnasios.set(l),
        error: (e) => this.error.set(mensajeDeError(e, 'No se pudieron cargar los gimnasios.')),
      });
    } else {
      this.cargar(null);
    }
  }

  /** Gimnasio que se envía en las escrituras: solo la plataforma lo indica; el Dueño usa el de su sesión. */
  private get destino(): string | null {
    return this.esPlataforma ? this.gimnasio.value : null;
  }

  cargar(gimnasioId: string | null): void {
    this.cargando.set(true);
    this.error.set(null);
    this.editando.set(null);
    this.api.obtener(gimnasioId).subscribe({
      next: (d) => { this.mostrar(d); this.cargando.set(false); },
      error: (e) => { this.error.set(mensajeDeError(e, 'No se pudo cargar el equipamiento.')); this.cargando.set(false); },
    });
  }

  /** El Dueño gestiona sus tipos propios; la plataforma, todos (el servidor vuelve a validar). */
  gestionaTipo(d: EquipamientoGimnasio, item: EquipamientoItem): boolean {
    return d.editable && (this.esPlataforma || item.propio);
  }

  agregarTipo(): void {
    const nombre = this.nuevoTipo.value.trim();
    if (!nombre) return;
    this.guardando.set(true);
    this.api.crearTipo(nombre, this.destino).subscribe({
      next: (item) => {
        // Queda marcado en la pantalla; se guarda con "Guardar".
        this.filas.update((l) => [...l, { item, marcado: true, cantidad: null }]);
        this.nuevoTipo.reset();
        this.guardando.set(false);
        this.snack.open(`Tipo "${item.nombre}" agregado. Recordá guardar.`, 'Cerrar', { duration: 4000 });
      },
      error: (e) => { this.snack.open(mensajeDeError(e), 'Cerrar', { duration: 6000 }); this.guardando.set(false); },
    });
  }

  renombrar(f: Fila): void {
    this.nombreEditado.setValue(f.item.nombre);
    this.editando.set(f.item.tipoEquipamientoId);
  }

  confirmarNombre(f: Fila): void {
    const nombre = this.nombreEditado.value.trim();
    if (!nombre) return;
    this.api.editarTipo(f.item.tipoEquipamientoId, nombre).subscribe({
      next: (item) => {
        this.filas.update((l) => l.map((x) => (x === f ? { ...x, item: { ...x.item, nombre: item.nombre } } : x)));
        this.editando.set(null);
        this.snack.open('Tipo renombrado', 'Cerrar', { duration: 3000 });
      },
      error: (e) => this.snack.open(mensajeDeError(e), 'Cerrar', { duration: 6000 }),
    });
  }

  darDeBajaTipo(f: Fila): void {
    const datos: ConfirmarDatos = {
      titulo: 'Dar de baja el tipo de equipamiento',
      mensaje: `"${f.item.nombre}" dejará de estar disponible y se desmarcará en los gimnasios que lo tengan. No se puede si algún ejercicio lo usa.`,
      confirmar: 'Dar de baja',
      peligro: true,
    };
    this.dialog.open(ConfirmarDialog, { data: datos }).afterClosed().subscribe((ok) => {
      if (!ok) return;
      this.api.darDeBajaTipo(f.item.tipoEquipamientoId).subscribe({
        next: () => {
          this.filas.update((l) => l.filter((x) => x !== f));
          this.snack.open('Tipo dado de baja', 'Cerrar', { duration: 3000 });
        },
        error: (e) => this.snack.open(mensajeDeError(e), 'Cerrar', { duration: 6000 }),
      });
    });
  }

  /** Previsualiza (O1) y, si hay ejercicios propios afectados, pide confirmación antes de guardar. */
  guardar(): void {
    const items: EquipamientoMarcado[] = this.filas()
      .filter((f) => f.marcado && !f.item.siempreDisponible)
      .map((f) => ({ tipoEquipamientoId: f.item.tipoEquipamientoId, cantidad: f.cantidad || null }));
    this.guardando.set(true);
    this.api.previsualizar(items, this.destino).subscribe({
      next: (p) => {
        if (p.cantidad === 0) { this.enviar(items); return; }
        this.dialog.open(ConfirmarDialog, { data: this.aviso(p) }).afterClosed().subscribe((ok) => {
          if (ok) this.enviar(items);
          else this.guardando.set(false);
        });
      },
      error: (e) => { this.snack.open(mensajeDeError(e), 'Cerrar', { duration: 6000 }); this.guardando.set(false); },
    });
  }

  private aviso(p: EquipamientoPrevisualizacion): ConfirmarDatos {
    const nombres = p.ejercicios.join(', ') + (p.cantidad > p.ejercicios.length ? ` y ${p.cantidad - p.ejercicios.length} más` : '');
    const quitados = p.tiposQuitados.length ? ` al quitar ${p.tiposQuitados.join(', ')}` : '';
    return {
      titulo: 'Ejercicios afectados',
      mensaje: `${p.cantidad} ${p.cantidad === 1 ? 'ejercicio propio queda' : 'ejercicios propios quedan'} sin poder usarse en la rutina baseline${quitados}: ${nombres}. ¿Guardar igual?`,
      confirmar: 'Guardar igual',
      peligro: true,
    };
  }

  private enviar(items: EquipamientoMarcado[]): void {
    this.api.guardar(items, this.destino).subscribe({
      next: (d) => {
        this.mostrar(d);
        this.guardando.set(false);
        this.snack.open('Equipamiento guardado', 'Cerrar', { duration: 3000 });
      },
      error: (e) => { this.snack.open(mensajeDeError(e), 'Cerrar', { duration: 6000 }); this.guardando.set(false); },
    });
  }

  private mostrar(d: EquipamientoGimnasio): void {
    this.datos.set(d);
    this.filas.set(d.items.map((item) => ({ item, marcado: item.marcado, cantidad: item.cantidad })));
  }
}
