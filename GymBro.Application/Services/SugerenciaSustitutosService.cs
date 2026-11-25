using System.Diagnostics;
using System.Text.Json;
using GymBro.Application.DTOs;
using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Domain.Exceptions;
using GymBro.Domain.Repositories;
using GymBro.Domain.Rules;
using GymBro.Domain.Security;

namespace GymBro.Application.Services
{
    /// <summary>Etapa 9b-2: pedido al modelo de lenguaje local (GymBro.IA /sugerir-sustitutos/).</summary>
    public sealed record PedidoSustitutos(EjercicioBuscadoDto Ejercicio, string Motivo, IReadOnlyList<EjercicioBuscadoDto> Candidatos, int Maximo);

    /// <summary>Texto crudo devuelto por el modelo, sin validar.</summary>
    public sealed record RespuestaGenerador(string Modelo, string Respuesta);

    /// <summary>Etapa 9b-2: cliente del generador (en produccion, HTTP a GymBro.IA; en pruebas, un doble).</summary>
    public interface IGeneradorSustitutos
    {
        /// <summary>Lanza ServicioExternoNoDisponibleException si la IA, Ollama o el modelo no estan disponibles.</summary>
        Task<RespuestaGenerador> GenerarAsync(PedidoSustitutos pedido);
    }

    public interface ISugerenciaSustitutosService
    {
        Task<SugerenciaSustitutosDto> SugerirAsync(Guid prescripcionId, Guid lineaId, SugerirSustitutosRequest request);
    }

    /// <summary>
    /// Etapa 9b-2 (caso a): sugerencia de sustitutos para una linea de una rutina pendiente de revision. Los candidatos los
    /// recupera la busqueda semantica con sus filtros duros (9b-1); el modelo local solo elige y justifica entre ellos. La
    /// salida se verifica EN CODIGO: debe ser JSON con la forma pedida y cada id elegido debe pertenecer al conjunto
    /// recuperado; si no, se descarta completa. Cada sustitucion se valida con IValidadorPrescripcionService (R-LIM). Es una
    /// sugerencia para el Dueno o el Personal Trainer: no modifica la rutina ni llega al Socio. No se permite sobre lineas
    /// del protocolo de piernas.
    /// </summary>
    public class SugerenciaSustitutosService : ISugerenciaSustitutosService
    {
        public const int CandidatosARecuperar = 10;
        public const int MaximoElegidos = 3;

        private readonly IPrescripcionEntrenamientoRepository _prescripciones;
        private readonly ISocioAccessService _acceso;
        private readonly IBuscadorSemanticoService _buscador;
        private readonly IGeneradorSustitutos _generador;
        private readonly IValidadorPrescripcionService _validador;
        private readonly IEjercicioRepository _ejercicios;
        private readonly IAuditService _audit;
        private readonly IUsuarioActual _actual;

        public SugerenciaSustitutosService(IPrescripcionEntrenamientoRepository prescripciones, ISocioAccessService acceso,
            IBuscadorSemanticoService buscador, IGeneradorSustitutos generador, IValidadorPrescripcionService validador,
            IEjercicioRepository ejercicios, IAuditService audit, IUsuarioActual actual)
        {
            _prescripciones = prescripciones;
            _acceso = acceso;
            _buscador = buscador;
            _generador = generador;
            _validador = validador;
            _ejercicios = ejercicios;
            _audit = audit;
            _actual = actual;
        }

        public async Task<SugerenciaSustitutosDto> SugerirAsync(Guid prescripcionId, Guid lineaId, SugerirSustitutosRequest request)
        {
            if (_actual.Rol is not (RolUsuario.AdministradorGimnasio or RolUsuario.Entrenador))
                throw new AccesoDenegadoException();
            var motivo = request?.Motivo?.Trim() ?? string.Empty;
            if (motivo.Length < 3 || motivo.Length > 300)
                throw new ReglaNegocioException("Indique el motivo de la sustitución (de 3 a 300 caracteres).");

            var rutina = await _prescripciones.GetForRevisionAsync(prescripcionId)
                ?? throw new RecursoNoEncontradoException("Rutina no encontrada.");
            await _acceso.AsegurarAccesoAsync(rutina.SocioId);
            if (rutina.Estado != EstadoPrescripcion.PendienteRevision)
                throw new ReglaNegocioException("Solo se sugieren sustitutos para rutinas pendientes de revisión.");

            var linea = rutina.EjerciciosPrescritos.FirstOrDefault(l => l.Id == lineaId && l.FechaBaja == null)
                ?? throw new RecursoNoEncontradoException("El ejercicio indicado no pertenece a la rutina.");
            if (rutina.ProtocoloCodigo is not null || linea.Rol is not null)
                throw new ReglaNegocioException(
                    "No se sugieren sustitutos para ejercicios del protocolo de piernas (piernas-1.0): rompería su lista de ejercicios y su trazabilidad.");

            var actual = linea.Ejercicio ?? await _ejercicios.GetByIdAsync(linea.EjercicioId)
                ?? throw new RecursoNoEncontradoException("Ejercicio no encontrado.");
            var enRutina = rutina.EjerciciosPrescritos.Where(l => l.FechaBaja == null).Select(l => l.EjercicioId).ToHashSet();

            // 1. Candidatos: busqueda semantica con filtros duros, para el gimnasio del socio, sin el ejercicio actual ni
            //    los que ya estan en la rutina.
            var recuperados = await _buscador.BuscarAsync(new BusquedaSemanticaRequest
            {
                Consulta = $"{actual.Nombre}. {actual.MusculoPrincipal}. Motivo: {motivo}",
                Cantidad = CandidatosARecuperar + enRutina.Count,
                Zona = string.IsNullOrWhiteSpace(actual.ZonaObjetivo) ? null : actual.ZonaObjetivo,
                GimnasioId = rutina.Socio?.GimnasioId,
            });
            var candidatos = recuperados.Where(c => !enRutina.Contains(c.Id)).Take(CandidatosARecuperar).ToList();
            var resultado = new SugerenciaSustitutosDto
            {
                PrescripcionId = rutina.Id, LineaId = linea.Id, EjercicioActualId = actual.Id, EjercicioActual = actual.Nombre,
                Candidatos = candidatos,
            };
            if (candidatos.Count == 0)
            {
                resultado.Descartada = true;
                resultado.MotivoDescarte = "sin_candidatos";
                await AuditarAsync(linea.Id, resultado);
                return resultado;
            }

            // 2. Modelo local (la excepcion de servicio no disponible sube como 503, sin efectos).
            var reloj = Stopwatch.StartNew();
            var respuesta = await _generador.GenerarAsync(new PedidoSustitutos(Mapear(actual), motivo, candidatos, MaximoElegidos));
            reloj.Stop();
            resultado.Modelo = respuesta.Modelo;
            resultado.LatenciaMs = Math.Round(reloj.Elapsed.TotalMilliseconds, 1);

            // 3. Verificacion en codigo: forma JSON y pertenencia de cada id al conjunto recuperado.
            var elegidos = Interpretar(respuesta.Respuesta);
            if (elegidos is null)
            {
                resultado.Descartada = true;
                resultado.MotivoDescarte = "salida_mal_formada";
            }
            else
            {
                var porId = candidatos.ToDictionary(c => c.Id);
                if (elegidos.Any(e => !porId.ContainsKey(e.Id)))
                {
                    resultado.Descartada = true;
                    resultado.MotivoDescarte = "candidato_fuera_del_conjunto";
                }
                else
                {
                    resultado.RespetaConjunto = true;
                    // 4. Cada sustitucion pasa por el validador de limites (R-LIM) con la rutina resultante.
                    foreach (var e in elegidos.DistinctBy(e => e.Id).Take(MaximoElegidos))
                    {
                        var violaciones = await ValidarSustitucionAsync(rutina, linea, e.Id);
                        if (violaciones.Count > 0)
                        {
                            resultado.RechazadasPorLimites.Add(new SustitutoRechazadoDto
                            {
                                EjercicioId = e.Id, Codigos = violaciones.Select(v => v.Codigo).Distinct().ToList(),
                            });
                            continue;
                        }
                        resultado.Sugerencias.Add(new SustitutoSugeridoDto
                        {
                            EjercicioId = e.Id, Nombre = porId[e.Id].Nombre, ZonaObjetivo = porId[e.Id].ZonaObjetivo,
                            Equipamiento = porId[e.Id].Equipamiento, Justificacion = e.Justificacion,
                        });
                    }
                }
            }

            await AuditarAsync(linea.Id, resultado);
            return resultado;
        }

        /// <summary>
        /// Interpreta {"elegidos": [{"id": "...", "justificacion": "..."}]}. Devuelve null si no es JSON con esa forma o si un
        /// id no es un GUID (un id inventado con otro formato tambien invalida la salida).
        /// </summary>
        public static List<(Guid Id, string Justificacion)>? Interpretar(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return null;
            try
            {
                using var doc = JsonDocument.Parse(texto);
                if (doc.RootElement.ValueKind != JsonValueKind.Object
                    || !doc.RootElement.TryGetProperty("elegidos", out var lista) || lista.ValueKind != JsonValueKind.Array)
                    return null;
                var salida = new List<(Guid, string)>();
                foreach (var e in lista.EnumerateArray())
                {
                    if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String
                        || !Guid.TryParse(id.GetString(), out var guid))
                        return null;
                    var just = e.TryGetProperty("justificacion", out var j) && j.ValueKind == JsonValueKind.String ? j.GetString()! : string.Empty;
                    salida.Add((guid, just.Length > 300 ? just[..300] : just));
                }
                return salida;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private async Task<IReadOnlyList<ViolacionLimite>> ValidarSustitucionAsync(PrescripcionEntrenamiento rutina, PrescripcionEjercicio linea, Guid sustitutoId)
        {
            var tipoSustituto = (await _ejercicios.GetByIdAsync(sustitutoId))?.TipoMedida ?? TipoMedida.Repeticiones;
            var lineas = rutina.EjerciciosPrescritos.Where(l => l.FechaBaja == null).OrderBy(l => l.Orden).Select(l =>
            {
                var esLinea = l.Id == linea.Id;
                return new LineaPrescripcion(esLinea ? sustitutoId : l.EjercicioId, l.Orden, string.Empty, l.SeriesObjetivo, l.RepeticionesMinimas,
                    l.RepeticionesMaximas, esLinea ? tipoSustituto : l.Ejercicio?.TipoMedida ?? TipoMedida.Repeticiones, l.RIRObjetivo, l.DescansoObjetivoSegundos);
            }).ToList();
            return _validador.Validar(lineas, rutina.Socio?.Nivel);
        }

        private Task AuditarAsync(Guid lineaId, SugerenciaSustitutosDto r) =>
            // Sin datos de salud ni texto libre: modelo, conteos, si respeto el conjunto y la latencia (medicion de la 9b-2).
            _audit.RegistrarAsync("SUGERENCIA_SUSTITUTOS", nameof(PrescripcionEjercicio), lineaId.ToString(),
                $"modelo={r.Modelo ?? "-"}; candidatos={r.Candidatos.Count}; sugeridos={r.Sugerencias.Count}; " +
                $"rechazados_limites={r.RechazadasPorLimites.Count}; respeta_conjunto={(r.RespetaConjunto ? "si" : "no")}; " +
                $"descarte={r.MotivoDescarte ?? "-"}; latencia_ms={r.LatenciaMs?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-"}");

        private static EjercicioBuscadoDto Mapear(Ejercicio e) => new()
        {
            Id = e.Id, Nombre = e.Nombre, ZonaObjetivo = e.ZonaObjetivo, MusculoPrincipal = e.MusculoPrincipal, Equipamiento = e.Equipamiento,
        };
    }
}
