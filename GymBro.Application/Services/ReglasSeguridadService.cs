using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Domain.Repositories;
using GymBro.Domain.Rules;

namespace GymBro.Application.Services
{
    /// <summary>R-LIM expuesto como servicio (Etapa 10a): lo usan el baseline y la revisión, y lo usarán 9b y 7c sin cambios.</summary>
    public interface IValidadorPrescripcionService
    {
        IReadOnlyList<ViolacionLimite> Validar(IReadOnlyCollection<LineaPrescripcion> lineas, NivelExperiencia? nivel);

        /// <summary>Atajo para una prescripción con sus líneas y el ejercicio de cada línea (tipo de medida y nombre).</summary>
        IReadOnlyList<ViolacionLimite> Validar(PrescripcionEntrenamiento prescripcion, NivelExperiencia? nivel);
    }

    public class ValidadorPrescripcionService : IValidadorPrescripcionService
    {
        private readonly ReglasSeguridadConfig _config;
        public ValidadorPrescripcionService(ReglasSeguridadConfig config) { _config = config; }

        public IReadOnlyList<ViolacionLimite> Validar(IReadOnlyCollection<LineaPrescripcion> lineas, NivelExperiencia? nivel) =>
            ValidadorPrescripcion.Validar(lineas, nivel is { } n && Enum.IsDefined(n) ? n : null, _config.Limites);

        public IReadOnlyList<ViolacionLimite> Validar(PrescripcionEntrenamiento prescripcion, NivelExperiencia? nivel) =>
            Validar(prescripcion.EjerciciosPrescritos.Where(e => e.FechaBaja == null).Select(e => new LineaPrescripcion(
                e.EjercicioId, e.Orden, e.Ejercicio?.Nombre, e.SeriesObjetivo, e.RepeticionesMinimas, e.RepeticionesMaximas,
                e.Ejercicio?.TipoMedida ?? TipoMedida.Repeticiones, e.RIRObjetivo, e.DescansoObjetivoSegundos)).ToList(), nivel);
    }

    public interface IReglasSeguridadService
    {
        /// <summary>Evalúa R-RIR-1 y R-PROG-1 sobre una sesión registrada. Idempotente.</summary>
        Task EvaluarSesionAsync(Guid sesionId);

        /// <summary>
        /// Evalúa R-FC-1 sobre el día del registro y, como su FC de reposo forma parte de la línea base de los días
        /// siguientes, también sobre los días con registro dentro de la ventana posterior (decisión P5). Idempotente.
        /// </summary>
        Task EvaluarBienestarAsync(Guid registroId);

        IReadOnlyList<ViolacionLimite> ValidarPrescripcion(IReadOnlyCollection<LineaPrescripcion> lineas, NivelExperiencia? nivel);
    }

    /// <summary>
    /// Motor de reglas deterministas de seguridad (Etapa 10a). Las reglas son funciones puras de GymBro.Domain.Rules; este
    /// servicio lee los datos, las aplica y sincroniza las alertas. Sin aprendizaje automático. Los umbrales son decisiones
    /// de diseño provisionales (ReglasSeguridadConfig). Quien lo invoca al guardar una sesión o un bienestar debe tolerar
    /// sus fallos (decisión D6): este servicio deja el contexto limpio y relanza la excepción.
    /// </summary>
    public class ReglasSeguridadService : IReglasSeguridadService
    {
        private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-AR");
        private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

        private readonly IAlertaSeguridadRepository _repo;
        private readonly IValidadorPrescripcionService _validador;
        private readonly ReglasSeguridadConfig _config;
        private readonly TimeProvider _reloj;

        public ReglasSeguridadService(IAlertaSeguridadRepository repo, IValidadorPrescripcionService validador,
            ReglasSeguridadConfig config, TimeProvider reloj)
        {
            _repo = repo;
            _validador = validador;
            _config = config;
            _reloj = reloj;
        }

        public IReadOnlyList<ViolacionLimite> ValidarPrescripcion(IReadOnlyCollection<LineaPrescripcion> lineas, NivelExperiencia? nivel) =>
            _validador.Validar(lineas, nivel);

        public async Task EvaluarSesionAsync(Guid sesionId)
        {
            try
            {
                var sesion = await _repo.GetSesionParaEvaluarAsync(sesionId);
                if (sesion is null) return;
                // P6: sin consentimiento para datos de salud no se generan alertas (la sesión ya quedó guardada).
                if (sesion.Socio.ConsentimientoDatosSaludFecha is null) return;

                var lineas = sesion.PrescripcionEntrenamiento?.EjerciciosPrescritos.Where(l => l.FechaBaja == null).ToList();
                foreach (var se in sesion.EjerciciosEjecutados.Where(e => e.FechaBaja == null))
                {
                    // Sin vínculo directo entre la sesión y la línea de la rutina: se relacionan por ejercicio
                    // (R-LIM no admite ejercicios repetidos en una rutina).
                    var linea = lineas?.FirstOrDefault(l => l.EjercicioId == se.EjercicioId);
                    var series = se.SeriesRealizadas.Where(s => s.FechaBaja == null)
                        .Select(s => new SerieEvaluada(s.PesoLevantadoKg, s.Repeticiones, s.RIR, s.EsCalentamiento)).ToList();
                    var nombre = se.Ejercicio?.Nombre ?? "Ejercicio";
                    var tipoMedida = se.Ejercicio?.TipoMedida ?? TipoMedida.Repeticiones;

                    var rir = linea is null ? null : ReglaRir.Evaluar(series, linea.RIRObjetivo, _config.Rir);
                    await SincronizarAsync(sesion.Socio, CodigosRegla.RirMayorAlPlanificado, se.Id.ToString(),
                        rir is { Alerta: true } ? ResultadoRir(sesion, se, nombre, series, rir) : null);

                    var sugerencia = linea is null ? null : ReglaProgresion.Evaluar(series,
                        new ObjetivoEjercicio(linea.SeriesObjetivo, linea.RepeticionesMinimas, linea.RepeticionesMaximas, linea.RIRObjetivo, tipoMedida),
                        rir is { Alerta: true }, _config.Progresion);
                    await SincronizarAsync(sesion.Socio, CodigosRegla.Progresion, se.Id.ToString(),
                        sugerencia is null ? null : ResultadoProgresion(sesion, se, nombre, linea!, tipoMedida, series, sugerencia));
                }

                await _repo.GuardarCambiosAsync();
            }
            catch
            {
                _repo.DescartarCambios();
                throw;
            }
        }

        public async Task EvaluarBienestarAsync(Guid registroId)
        {
            try
            {
                var dias = _config.FcReposo.DiasLineaBase;
                var registro = await _repo.GetRegistroBienestarAsync(registroId);
                if (registro is null) return;
                var socio = registro.Socio;
                // Línea base del día (los N anteriores) y días posteriores dentro de la ventana, con sus propias líneas base.
                var registros = await _repo.GetBienestarAsync(registro.SocioId, registro.Fecha.AddDays(-dias), registro.Fecha.AddDays(dias));

                var porFecha = registros.ToDictionary(r => r.Fecha);
                var aEvaluar = registros.Where(r => r.Fecha >= registro.Fecha && r.Fecha <= registro.Fecha.AddDays(dias)).ToList();
                foreach (var r in aEvaluar)
                {
                    var previos = Enumerable.Range(1, dias)
                        .Select(i => porFecha.TryGetValue(r.Fecha.AddDays(-i), out var p) ? p.FrecuenciaCardiacaReposo : null);
                    var fc = ReglaFcReposo.Evaluar(r.FrecuenciaCardiacaReposo, previos, _config.FcReposo);
                    await SincronizarAsync(socio, CodigosRegla.FcReposo, r.Id.ToString(),
                        fc is { Alerta: true } ? ResultadoFc(socio, r, fc) : null);
                }

                await _repo.GuardarCambiosAsync();
            }
            catch
            {
                _repo.DescartarCambios();
                throw;
            }
        }

        // ---- Sincronización idempotente ----

        private sealed record Resultado(TipoAlerta Tipo, SeveridadAlerta Severidad, string Mensaje, object Detalle, Guid? SesionId,
            Guid? SesionEjercicioId, Guid? RegistroBienestarId);

        /// <summary>
        /// Una fila por (socio, regla, origen). Sin resultado: retira la vigente (baja lógica). Con resultado: crea, reactiva
        /// o actualiza; si el resultado no cambió, no toca la fila (una alerta vista sigue vista).
        /// </summary>
        private async Task SincronizarAsync(Socio socio, string regla, string clave, Resultado? r)
        {
            var existente = await _repo.GetPorClaveAsync(socio.Id, regla, clave);
            var ahora = _reloj.GetUtcNow().UtcDateTime;

            if (r is null)
            {
                if (existente is { FechaBaja: null })
                {
                    existente.FechaBaja = ahora;
                    existente.FechaModificacion = ahora;
                }
                return;
            }

            var detalle = JsonSerializer.Serialize(r.Detalle, Json);
            if (existente is null)
            {
                _repo.Agregar(new AlertaSeguridad
                {
                    SocioId = socio.Id, GimnasioId = socio.GimnasioId, ReglaCodigo = regla, ClaveOrigen = clave,
                    SesionId = r.SesionId, SesionEjercicioId = r.SesionEjercicioId, RegistroBienestarId = r.RegistroBienestarId,
                    Tipo = r.Tipo, Severidad = r.Severidad, Mensaje = r.Mensaje, Detalle = detalle,
                    Estado = EstadoAlerta.Pendiente, FechaAlta = ahora,
                });
                return;
            }

            var igual = existente.FechaBaja is null && existente.Tipo == r.Tipo && existente.Severidad == r.Severidad
                        && existente.Mensaje == r.Mensaje && JsonCanonico(existente.Detalle) == JsonCanonico(detalle);
            if (igual) return;

            existente.FechaBaja = null;
            existente.Tipo = r.Tipo;
            existente.Severidad = r.Severidad;
            existente.Mensaje = r.Mensaje;
            existente.Detalle = detalle;
            existente.Estado = EstadoAlerta.Pendiente;      // cambió el resultado o reapareció: vuelve a pendiente
            existente.VistaPorUsuarioId = null;
            existente.VistaEn = null;
            existente.FechaModificacion = ahora;
        }

        /// <summary>
        /// JSON comparable: jsonb reordena las claves y cambia los espacios al guardar, así que se compara con las claves
        /// ordenadas y sin espacios. Los números conservan su texto (jsonb conserva la escala de numeric).
        /// </summary>
        public static string JsonCanonico(string json)
        {
            static JsonNode? Ordenar(JsonNode? n) => n switch
            {
                JsonObject o => new JsonObject(o.OrderBy(p => p.Key, StringComparer.Ordinal)
                    .Select(p => KeyValuePair.Create(p.Key, Ordenar(p.Value?.DeepClone())))),
                JsonArray a => new JsonArray(a.Select(x => Ordenar(x?.DeepClone())).ToArray()),
                _ => n?.DeepClone()
            };
            return Ordenar(JsonNode.Parse(json))?.ToJsonString() ?? "null";
        }

        // ---- Resultados por regla (mensaje simple y detalle auditable) ----

        private Resultado ResultadoRir(SesionEntrenamiento sesion, SesionEjercicio se, string nombre, List<SerieEvaluada> series, EvaluacionRir rir) =>
            new(TipoAlerta.Alerta, SeveridadAlerta.Advertencia,
                $"{nombre}: llegaste mucho más cerca del fallo que lo planificado. La próxima vez dejá más margen.",
                new
                {
                    regla = CodigosRegla.RirMayorAlPlanificado,
                    reglas_version = ReglasSeguridadConfig.ReglasVersion,
                    ejercicio = nombre,
                    entradas = new
                    {
                        rir_objetivo = rir.RirObjetivo,
                        rir_series_trabajo = series.Where(s => !s.EsCalentamiento).Select(s => s.Rir).ToList(),
                    },
                    umbrales = new { diferencia_minima = _config.Rir.DiferenciaMinima, series_minimas_con_dato = _config.Rir.SeriesMinimasConDato },
                    resultado = new { mediana_rir = rir.MedianaRealizada, series_con_dato = rir.SeriesConDato, diferencia = rir.Diferencia },
                }, sesion.Id, se.Id, null);

        private Resultado ResultadoProgresion(SesionEntrenamiento sesion, SesionEjercicio se, string nombre, PrescripcionEjercicio linea,
            TipoMedida tipoMedida, List<SerieEvaluada> series, SugerenciaCarga s)
        {
            var verbo = s.Direccion == DireccionCarga.Subir ? "subir" : "bajar";
            var p = _config.Progresion;
            return new(TipoAlerta.Sugerencia, SeveridadAlerta.Informativa,
                $"{nombre}: próxima vez, {Kg(s.CargaSugeridaKg)} kg ({verbo} desde {Kg(s.CargaActualKg)} kg).",
                new
                {
                    regla = CodigosRegla.Progresion,
                    reglas_version = ReglasSeguridadConfig.ReglasVersion,
                    ejercicio = nombre,
                    entradas = new
                    {
                        tipo_medida = tipoMedida.ToString(),
                        series_objetivo = linea.SeriesObjetivo,
                        repeticiones_minimas = linea.RepeticionesMinimas,
                        repeticiones_maximas = linea.RepeticionesMaximas,
                        rir_objetivo = linea.RIRObjetivo,
                        series_trabajo = series.Where(x => !x.EsCalentamiento)
                            .Select(x => new { peso_kg = x.PesoKg, repeticiones = x.Repeticiones, rir = x.Rir }).ToList(),
                    },
                    umbrales = new
                    {
                        incremento_porcentaje = p.IncrementoPorcentaje, decremento_porcentaje = p.DecrementoPorcentaje,
                        redondeo_kg = p.RedondeoKg, cambio_minimo_kg = p.CambioMinimoKg, tope_porcentaje = p.TopePorcentaje,
                    },
                    resultado = new
                    {
                        direccion = s.Direccion.ToString(), carga_actual_kg = s.CargaActualKg, carga_sugerida_kg = s.CargaSugeridaKg,
                        cambio_kg = s.CambioKg, cambio_porcentaje = s.CambioPorcentaje,
                    },
                }, sesion.Id, se.Id, null);
        }

        private Resultado ResultadoFc(Socio socio, RegistroBienestarDiario r, EvaluacionFcReposo fc)
        {
            var c = _config.FcReposo;
            return new(TipoAlerta.Alerta, SeveridadAlerta.Advertencia,
                $"FC de reposo más alta que tu referencia (+{fc.VariacionPorcentaje.ToString("0.#", Es)} %); considerá bajar la intensidad hoy.",
                new
                {
                    regla = CodigosRegla.FcReposo,
                    reglas_version = ReglasSeguridadConfig.ReglasVersion,
                    entradas = new { fecha = r.Fecha.ToString("yyyy-MM-dd"), fc_reposo_lpm = fc.ValorDia },
                    umbrales = new
                    {
                        dias_linea_base = c.DiasLineaBase, registros_minimos = c.RegistrosMinimos,
                        umbral_porcentaje = c.UmbralPorcentaje, umbral_lpm = c.UmbralLpm,
                    },
                    resultado = new
                    {
                        linea_base_lpm = fc.LineaBase, registros_linea_base = fc.RegistrosLineaBase,
                        diferencia_lpm = fc.DiferenciaLpm, variacion_porcentaje = fc.VariacionPorcentaje,
                    },
                }, null, null, r.Id);
        }

        private static string Kg(decimal kg) => kg.ToString("0.#", Es);
    }
}
