import { Component, DestroyRef, forwardRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ControlValueAccessor, FormControl, NG_VALUE_ACCESSOR, ReactiveFormsModule } from '@angular/forms';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { catchError, debounceTime, distinctUntilChanged, filter, map, of, switchMap } from 'rxjs';
import { EjerciciosApi } from '../../core/api/ejercicios.api';
import { EjercicioResumen } from '../../core/models/ejercicio.models';

/**
 * Selector de un ejercicio del catálogo con autocompletado.
 * El valor del control es el EjercicioResumen elegido, o null si no se eligió ninguno.
 */
@Component({
  selector: 'app-ejercicio-selector',
  imports: [ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatAutocompleteModule],
  providers: [{ provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => EjercicioSelector), multi: true }],
  template: `
    <mat-form-field class="campo">
      <mat-label>Ejercicio</mat-label>
      <input matInput [formControl]="entrada" [matAutocomplete]="auto" (blur)="alSalir()" />
      <mat-autocomplete #auto [displayWith]="mostrar" (optionSelected)="elegir($event.option.value)">
        @for (e of opciones(); track e.id) {
          <mat-option [value]="e">
            <span>{{ e.nombre }}</span>
            <small class="meta">{{ e.zonaObjetivo }} · {{ e.equipamiento }}</small>
          </mat-option>
        }
      </mat-autocomplete>
      <mat-hint>Escribí al menos 2 letras</mat-hint>
    </mat-form-field>
  `,
  styles: `
    .campo { width: 100%; }
    .meta { display: block; color: var(--mat-sys-on-surface-variant); }
  `,
})
export class EjercicioSelector implements ControlValueAccessor {
  private readonly api = inject(EjerciciosApi);

  readonly entrada = new FormControl<string | EjercicioResumen>('', { nonNullable: true });
  readonly opciones = signal<EjercicioResumen[]>([]);

  private seleccionado: EjercicioResumen | null = null;
  private onChange: (v: EjercicioResumen | null) => void = () => {};
  private onTouched: () => void = () => {};

  readonly mostrar = (e: EjercicioResumen | string | null) => (typeof e === 'string' ? e : (e?.nombre ?? ''));

  constructor() {
    this.entrada.valueChanges
      .pipe(
        filter((v): v is string => typeof v === 'string'),
        map((v) => v.trim()),
        debounceTime(250),
        distinctUntilChanged(),
        switchMap((texto) =>
          texto.length < 2
            ? of([])
            : this.api.catalogo({ texto, pagina: 1, tamanioPagina: 15 }).pipe(
                map((r) => r.items),
                catchError(() => of([])),
              ),
        ),
        takeUntilDestroyed(inject(DestroyRef)),
      )
      .subscribe((items) => this.opciones.set(items));

    // Si el usuario edita el texto después de elegir, la selección deja de ser válida.
    this.entrada.valueChanges.pipe(takeUntilDestroyed(inject(DestroyRef))).subscribe((v) => {
      if (typeof v === 'string' && this.seleccionado) {
        this.seleccionado = null;
        this.onChange(null);
      }
    });
  }

  elegir(e: EjercicioResumen): void {
    this.seleccionado = e;
    this.onChange(e);
  }

  alSalir(): void {
    this.onTouched();
  }

  writeValue(valor: EjercicioResumen | null): void {
    this.seleccionado = valor;
    this.entrada.setValue(valor ?? '', { emitEvent: false });
  }

  registerOnChange(fn: (v: EjercicioResumen | null) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(deshabilitado: boolean): void {
    if (deshabilitado) this.entrada.disable({ emitEvent: false });
    else this.entrada.enable({ emitEvent: false });
  }
}
