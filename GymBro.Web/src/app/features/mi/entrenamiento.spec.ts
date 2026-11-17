import { RutinaPendiente } from '../../core/models/acceso.models';
import { Sesion } from '../../core/models/sesion.models';
import {
  agregarSerie, alternarSerie, borradorValido, construirSesionDesdeEntrenamiento, crearEntrenamiento, editarSerie,
  quitarSerie, rutinaEnRevision, rutinaVigente, seriesHechas, ultimasSeries,
} from './entrenamiento';

const T0 = Date.UTC(2026, 10, 16, 18, 0, 0);

function rutina(id: string, estado: string, fechaAlta: string): RutinaPendiente {
  return {
    id, socioId: 's1', nombreSocio: 'Ana', origenGeneracion: 'BASELINE_DETERMINISTA', observaciones: null, estado, fechaAlta,
    ejercicios: [
      { id: 'pe2', ejercicioId: 'ej-remo', orden: 2, ejercicio: 'Remo', seriesObjetivo: 2, repeticionesMinimas: 10,
        repeticionesMaximas: 12, rirObjetivo: 2, descansoObjetivoSegundos: 60, observaciones: null },
      { id: 'pe1', ejercicioId: 'ej-press', orden: 1, ejercicio: 'Press', seriesObjetivo: 3, repeticionesMinimas: 8,
        repeticionesMaximas: 12, rirObjetivo: 2, descansoObjetivoSegundos: 90, observaciones: null },
    ],
  };
}

function sesion(inicio: string, ejercicioId: string, series: [number, number, boolean][]): Sesion {
  return {
    id: inicio, socioId: 's1', prescripcionEntrenamientoId: null, inicio, fin: null,
    caloriasQuemadas: 0, frecuenciaCardiacaMaxima: 0, frecuenciaCardiacaPromedio: 0,
    ejercicios: [{
      id: 'x', ejercicioId, orden: 1, observaciones: null,
      seriesRealizadas: series.map(([peso, reps, calentamiento], i) => ({
        id: `${i}`, ordenSerie: i + 1, pesoLevantadoKg: peso, repeticiones: reps, rir: null,
        esCalentamiento: calentamiento, tiempoDescansoSegundos: null,
      })),
    }],
  };
}

describe('rutina vigente', () => {
  it('toma la más reciente Aprobada o Editada e ignora pendientes y rechazadas', () => {
    const lista = [
      rutina('vieja', 'Aprobada', '2026-11-01T10:00:00Z'),
      rutina('editada', 'Editada', '2026-11-10T10:00:00Z'),
      rutina('rechazada', 'Rechazada', '2026-11-12T10:00:00Z'),
      rutina('pendiente', 'PendienteRevision', '2026-11-15T10:00:00Z'),
    ];
    expect(rutinaVigente(lista)?.id).toBe('editada');
    expect(rutinaEnRevision(lista)?.id).toBe('pendiente');
  });

  it('sin rutina vigente devuelve null', () => {
    expect(rutinaVigente([rutina('r', 'Rechazada', '2026-11-12T10:00:00Z')])).toBeNull();
    expect(rutinaVigente([])).toBeNull();
  });
});

describe('Anterior', () => {
  it('usa la última sesión de cada ejercicio y descarta el calentamiento', () => {
    const mapa = ultimasSeries([
      sesion('2026-11-01T10:00:00Z', 'ej-press', [[30, 10, false]]),
      sesion('2026-11-08T10:00:00Z', 'ej-press', [[20, 12, true], [40, 9, false], [42.5, 8, false]]),
    ]);
    expect(mapa.get('ej-press')).toEqual([
      { pesoLevantadoKg: 40, repeticiones: 9, rir: null },
      { pesoLevantadoKg: 42.5, repeticiones: 8, rir: null },
    ]);
    expect(mapa.has('ej-remo')).toBe(false);
  });
});

describe('entrenamiento en curso', () => {
  const anterior = ultimasSeries([sesion('2026-11-08T10:00:00Z', 'ej-press', [[40, 9, false]])]);
  const nuevo = () => crearEntrenamiento('s1', rutina('p1', 'Aprobada', '2026-11-10T10:00:00Z'), anterior, T0);

  it('precarga las series objetivo en orden de la rutina, con lo anterior si existe', () => {
    const e = nuevo();
    expect(e.prescripcionId).toBe('p1');
    expect(e.ejercicios.map((x) => x.nombre)).toEqual(['Press', 'Remo']);
    expect(e.ejercicios[0].series.length).toBe(3);
    expect(e.ejercicios[0].series[0]).toMatchObject({ pesoKg: 40, repeticiones: 9, rir: null, hecha: false });
    expect(e.ejercicios[0].series[1]).toMatchObject({ pesoKg: null, repeticiones: 8 });
  });

  it('el ✔ marca la serie y arranca el descanso prescripto', () => {
    const e = alternarSerie(nuevo(), 0, 0, T0 + 1000);
    expect(e.ejercicios[0].series[0].hecha).toBe(true);
    expect(e.descanso?.finEpoch).toBe(T0 + 1000 + 90_000);
  });

  it('no deja marcar una serie sin peso o con RIR fuera de 0–5', () => {
    const sinPeso = alternarSerie(nuevo(), 0, 1, T0);
    expect(sinPeso.ejercicios[0].series[1].hecha).toBe(false);

    const rirMalo = alternarSerie(editarSerie(nuevo(), 0, 0, { rir: 6 }), 0, 0, T0);
    expect(rirMalo.ejercicios[0].series[0].hecha).toBe(false);
  });

  it('+ serie copia la última y quitar no borra series hechas', () => {
    let e = agregarSerie(nuevo(), 0);
    expect(e.ejercicios[0].series.length).toBe(4);
    e = alternarSerie(e, 0, 0, T0);
    e = quitarSerie(e, 0, 0);
    expect(e.ejercicios[0].series.length).toBe(4);
    e = quitarSerie(e, 0, 3);
    expect(e.ejercicios[0].series.length).toBe(3);
  });

  it('reconoce si el borrador guardado corresponde a la rutina vigente', () => {
    expect(borradorValido(nuevo(), 's1', 'p1')).toBe(true);
    expect(borradorValido(nuevo(), 's1', 'otra')).toBe(false);
    expect(borradorValido(null, 's1', 'p1')).toBe(false);
  });
});

describe('request de la sesión', () => {
  const anterior = ultimasSeries([]);
  const base = () => crearEntrenamiento('s1', rutina('p1', 'Aprobada', '2026-11-10T10:00:00Z'), anterior, T0);

  it('solo envía las series con ✔, vinculadas a la prescripción, con descanso real', () => {
    let e = editarSerie(base(), 0, 0, { pesoKg: 40, repeticiones: 10, rir: 2 });
    e = editarSerie(e, 0, 1, { pesoKg: 40, repeticiones: 9 });
    e = editarSerie(e, 0, 2, { pesoKg: 40, repeticiones: 8 }); // sin ✔: no se envía
    e = alternarSerie(e, 0, 0, T0 + 60_000);
    e = alternarSerie(e, 0, 1, T0 + 180_000);

    const r = construirSesionDesdeEntrenamiento(e, new Date(T0 + 3_600_000));

    expect(seriesHechas(e)).toBe(2);
    expect(r.prescripcionEntrenamientoId).toBe('p1');
    expect(r.inicio).toBe(new Date(T0).toISOString());
    expect(r.ejercicios.length).toBe(1); // Remo no tiene series hechas
    expect(r.ejercicios[0]).toMatchObject({ ejercicioId: 'ej-press', orden: 1 });
    expect(r.ejercicios[0].seriesRealizadas).toEqual([
      { ordenSerie: 1, pesoLevantadoKg: 40, repeticiones: 10, rir: 2, esCalentamiento: false, tiempoDescansoSegundos: null },
      { ordenSerie: 2, pesoLevantadoKg: 40, repeticiones: 9, rir: null, esCalentamiento: false, tiempoDescansoSegundos: 120 },
    ]);
  });

  it('el RIR enviado queda entre 0 y 5', () => {
    let e = editarSerie(base(), 0, 0, { pesoKg: 40, repeticiones: 10, rir: 0 });
    e = editarSerie(e, 0, 1, { pesoKg: 40, repeticiones: 10, rir: 5 });
    e = alternarSerie(alternarSerie(e, 0, 0, T0), 0, 1, T0 + 1000);
    const rirs = construirSesionDesdeEntrenamiento(e).ejercicios[0].seriesRealizadas.map((s) => s.rir);
    expect(rirs).toEqual([0, 5]);

    // Una serie hecha que luego queda con RIR inválido (p. ej. borrador alterado) no se envía.
    const alterado = editarSerie(e, 0, 1, { rir: 7 });
    expect(() => construirSesionDesdeEntrenamiento(alterado)).toThrow(/RIR/);
  });

  it('sin series hechas no se puede finalizar', () => {
    expect(() => construirSesionDesdeEntrenamiento(base())).toThrow(/al menos una serie/);
  });
});
