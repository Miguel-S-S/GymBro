import { RutinaPendiente } from '../../core/models/acceso.models';
import { RegistrarSesionRequest, Sesion } from '../../core/models/sesion.models';
import { EstadoTemporizador, iniciarDescanso } from '../../core/util/temporizador';

/**
 * Modelo del entrenamiento en curso del Socio (Etapa 8, pantalla "Entrenar").
 * Funciones puras: la pantalla solo guarda el estado y llama a estas funciones.
 * Prescripción ≠ Sesión ≠ SerieRealizada: la rutina prescribe por ejercicio; la ejecución se registra por serie.
 */

/** Decisión del 16/11/2026: una rutina es vigente si está Aprobada o Editada (aprobada con ajustes). */
export const ESTADOS_VIGENTES: readonly string[] = ['Aprobada', 'Editada'];
export const RIR_MINIMO = 0;
export const RIR_MAXIMO = 5;

export interface SerieEnCurso {
  pesoKg: number | null;
  repeticiones: number | null;
  rir: number | null;
  /** ✔: solo las series marcadas se envían a la API. */
  hecha: boolean;
  /** Momento del ✔ (ms epoch): sirve para calcular el descanso real. */
  completadaEn: number | null;
}

export interface EjercicioEnCurso {
  prescripcionEjercicioId: string;
  ejercicioId: string;
  nombre: string;
  orden: number;
  seriesObjetivo: number;
  repeticionesMinimas: number;
  repeticionesMaximas: number;
  rirObjetivo: number | null;
  descansoObjetivoSegundos: number;
  /** Última ejecución de este ejercicio (series efectivas), para la columna "Anterior". */
  anterior: SerieAnterior[];
  series: SerieEnCurso[];
}

export interface Entrenamiento {
  version: 1;
  socioId: string;
  prescripcionId: string;
  /** ISO 8601 (UTC). */
  inicio: string;
  ejercicios: EjercicioEnCurso[];
  descanso: EstadoTemporizador | null;
}

export interface SerieAnterior {
  pesoLevantadoKg: number;
  repeticiones: number;
  rir: number | null;
}

// ---------- Rutina vigente ----------

const porFechaDesc = (a: RutinaPendiente, b: RutinaPendiente) => b.fechaAlta.localeCompare(a.fechaAlta);

/** La rutina vigente más reciente (Aprobada o Editada), o null. */
export function rutinaVigente(rutinas: RutinaPendiente[]): RutinaPendiente | null {
  return [...rutinas].sort(porFechaDesc).find((r) => ESTADOS_VIGENTES.includes(r.estado)) ?? null;
}

/** La rutina pendiente de revisión, si hay una. */
export function rutinaEnRevision(rutinas: RutinaPendiente[]): RutinaPendiente | null {
  return [...rutinas].sort(porFechaDesc).find((r) => r.estado === 'PendienteRevision') ?? null;
}

// ---------- "Anterior" (calculado en el cliente con el historial; sin endpoint nuevo) ----------

/**
 * Para cada ejercicio, las series efectivas (no de calentamiento) de la última sesión en que se hizo,
 * ordenadas por número de serie.
 */
export function ultimasSeries(sesiones: Sesion[]): Map<string, SerieAnterior[]> {
  const resultado = new Map<string, SerieAnterior[]>();
  const recientes = [...sesiones].sort((a, b) => b.inicio.localeCompare(a.inicio));
  for (const sesion of recientes) {
    for (const ej of sesion.ejercicios) {
      if (resultado.has(ej.ejercicioId)) continue;
      const efectivas = ej.seriesRealizadas
        .filter((s) => !s.esCalentamiento)
        .sort((a, b) => a.ordenSerie - b.ordenSerie)
        .map((s) => ({ pesoLevantadoKg: s.pesoLevantadoKg, repeticiones: s.repeticiones, rir: s.rir }));
      if (efectivas.length) resultado.set(ej.ejercicioId, efectivas);
    }
  }
  return resultado;
}

// ---------- Creación y edición ----------

/**
 * Arma el entrenamiento con las series objetivo precargadas: peso y repeticiones de la vez anterior si existen;
 * si no, peso vacío y el mínimo del rango de repeticiones. El RIR queda vacío (es opcional).
 */
export function crearEntrenamiento(
  socioId: string,
  rutina: RutinaPendiente,
  anterior: Map<string, SerieAnterior[]>,
  ahora: number = Date.now(),
): Entrenamiento {
  return {
    version: 1,
    socioId,
    prescripcionId: rutina.id,
    inicio: new Date(ahora).toISOString(),
    descanso: null,
    ejercicios: [...rutina.ejercicios]
      .sort((a, b) => a.orden - b.orden)
      .map((e) => {
        const previas = anterior.get(e.ejercicioId) ?? [];
        return {
          prescripcionEjercicioId: e.id,
          ejercicioId: e.ejercicioId,
          nombre: e.ejercicio,
          orden: e.orden,
          seriesObjetivo: e.seriesObjetivo,
          repeticionesMinimas: e.repeticionesMinimas,
          repeticionesMaximas: e.repeticionesMaximas,
          rirObjetivo: e.rirObjetivo,
          descansoObjetivoSegundos: e.descansoObjetivoSegundos,
          anterior: previas,
          series: Array.from({ length: Math.max(1, e.seriesObjetivo) }, (_, i) => ({
            pesoKg: previas[i]?.pesoLevantadoKg ?? null,
            repeticiones: previas[i]?.repeticiones ?? e.repeticionesMinimas,
            rir: null,
            hecha: false,
            completadaEn: null,
          })),
        };
      }),
  };
}

/** ¿El borrador guardado sigue sirviendo para la rutina vigente? */
export function borradorValido(b: Entrenamiento | null, socioId: string, prescripcionId: string): b is Entrenamiento {
  return !!b && b.version === 1 && b.socioId === socioId && b.prescripcionId === prescripcionId && Array.isArray(b.ejercicios);
}

function conSerie(e: Entrenamiento, iEj: number, iSerie: number, cambio: (s: SerieEnCurso) => SerieEnCurso): Entrenamiento {
  return {
    ...e,
    ejercicios: e.ejercicios.map((ej, i) =>
      i !== iEj ? ej : { ...ej, series: ej.series.map((s, j) => (j === iSerie ? cambio(s) : s)) },
    ),
  };
}

export function editarSerie(
  e: Entrenamiento, iEj: number, iSerie: number, cambios: Partial<Pick<SerieEnCurso, 'pesoKg' | 'repeticiones' | 'rir'>>,
): Entrenamiento {
  return conSerie(e, iEj, iSerie, (s) => ({ ...s, ...cambios }));
}

/** Motivo por el que una serie no puede marcarse con ✔, o null si es válida. */
export function motivoSerieInvalida(s: SerieEnCurso): string | null {
  if (s.pesoKg === null || Number.isNaN(s.pesoKg) || s.pesoKg < 0) return 'Indicá el peso (0 o más kg).';
  if (s.repeticiones === null || !Number.isInteger(s.repeticiones) || s.repeticiones < 1) return 'Indicá las repeticiones (1 o más).';
  if (s.rir !== null && (!Number.isInteger(s.rir) || s.rir < RIR_MINIMO || s.rir > RIR_MAXIMO))
    return `El RIR debe estar entre ${RIR_MINIMO} y ${RIR_MAXIMO}.`;
  return null;
}

/**
 * ✔ / desmarcar. Al marcar, arranca el descanso con el valor prescripto del ejercicio.
 * Si la serie no es válida, devuelve el entrenamiento sin cambios (la pantalla muestra el motivo).
 */
export function alternarSerie(e: Entrenamiento, iEj: number, iSerie: number, ahora: number = Date.now()): Entrenamiento {
  const serie = e.ejercicios[iEj]?.series[iSerie];
  if (!serie) return e;
  if (serie.hecha) return conSerie(e, iEj, iSerie, (s) => ({ ...s, hecha: false, completadaEn: null }));
  if (motivoSerieInvalida(serie)) return e;
  const marcado = conSerie(e, iEj, iSerie, (s) => ({ ...s, hecha: true, completadaEn: ahora }));
  return { ...marcado, descanso: iniciarDescanso(e.ejercicios[iEj].descansoObjetivoSegundos, ahora) };
}

/** "+ serie": copia peso y repeticiones de la última serie del ejercicio. */
export function agregarSerie(e: Entrenamiento, iEj: number): Entrenamiento {
  return {
    ...e,
    ejercicios: e.ejercicios.map((ej, i) => {
      if (i !== iEj) return ej;
      const ultima = ej.series[ej.series.length - 1];
      const nueva: SerieEnCurso = {
        pesoKg: ultima?.pesoKg ?? null,
        repeticiones: ultima?.repeticiones ?? ej.repeticionesMinimas,
        rir: null,
        hecha: false,
        completadaEn: null,
      };
      return { ...ej, series: [...ej.series, nueva] };
    }),
  };
}

/** Quita una serie que todavía no se marcó. */
export function quitarSerie(e: Entrenamiento, iEj: number, iSerie: number): Entrenamiento {
  return {
    ...e,
    ejercicios: e.ejercicios.map((ej, i) =>
      i !== iEj || ej.series[iSerie]?.hecha || ej.series.length <= 1 ? ej : { ...ej, series: ej.series.filter((_, j) => j !== iSerie) },
    ),
  };
}

export function seriesHechas(e: Entrenamiento): number {
  return e.ejercicios.reduce((n, ej) => n + ej.series.filter((s) => s.hecha).length, 0);
}

// ---------- Request de la API ----------

/**
 * Arma el POST /api/Sesiones de la sesión vinculada a la prescripción.
 * - Solo se envían las series con ✔; se renumeran 1..n dentro de cada ejercicio.
 * - Los ejercicios sin series hechas no se envían.
 * - Descanso real (aproximación): segundos entre el ✔ anterior del mismo ejercicio y este ✔ (incluye la ejecución
 *   de la serie); la primera serie de cada ejercicio va sin descanso (null).
 * - El RIR debe estar entre 0 y 5 (la API y la base lo validan igual).
 * Lanza Error si no hay ninguna serie hecha o si alguna serie hecha es inválida.
 */
export function construirSesionDesdeEntrenamiento(e: Entrenamiento, fin: Date = new Date()): RegistrarSesionRequest {
  const ejercicios = e.ejercicios
    .map((ej) => {
      const hechas = ej.series.filter((s) => s.hecha).sort((a, b) => (a.completadaEn ?? 0) - (b.completadaEn ?? 0));
      return {
        ejercicioId: ej.ejercicioId,
        orden: ej.orden,
        observaciones: null,
        seriesRealizadas: hechas.map((s, i) => {
          const motivo = motivoSerieInvalida(s);
          if (motivo) throw new Error(`${ej.nombre}, serie ${i + 1}: ${motivo}`);
          const previa = hechas[i - 1];
          return {
            ordenSerie: i + 1,
            pesoLevantadoKg: s.pesoKg as number,
            repeticiones: s.repeticiones as number,
            rir: s.rir,
            esCalentamiento: false,
            tiempoDescansoSegundos:
              previa?.completadaEn && s.completadaEn ? Math.max(0, Math.round((s.completadaEn - previa.completadaEn) / 1000)) : null,
          };
        }),
      };
    })
    .filter((ej) => ej.seriesRealizadas.length > 0);

  if (ejercicios.length === 0) throw new Error('Marcá con ✔ al menos una serie antes de finalizar.');

  return {
    socioId: e.socioId,
    prescripcionEntrenamientoId: e.prescripcionId,
    inicio: e.inicio,
    fin: fin.toISOString(),
    caloriasQuemadas: null,
    frecuenciaCardiacaMaxima: null,
    frecuenciaCardiacaPromedio: null,
    ejercicios,
  };
}
