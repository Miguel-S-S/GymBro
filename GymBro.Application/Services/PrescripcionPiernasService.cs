using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using GymBro.Application.Abstractions;
using GymBro.Application.DTOs.Acceso;
using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Domain.Exceptions;
using GymBro.Domain.Repositories;
using GymBro.Domain.Rules;

namespace GymBro.Application.Services
{
    public interface IPrescripcionPiernasService
    {
        /// <summary>
        /// Genera la rutina de piernas determinista del protocolo (Etapa 7d) y la deja en revisión o aprobada según la
        /// política del gimnasio.
        /// </summary>
        Task<RutinaPendienteDto> GenerarAsync(Guid socioId);

        /// <summary>
        /// Rutinas del socio (todas, la más reciente primero), con los datos del protocolo por línea (rol, grupo, tempo y
        /// carga sugerida). Mismo contenido que el listado del baseline más los campos aditivos de la Etapa 7d.
        /// </summary>
        Task<IEnumerable<RutinaPendienteDto>> ListarDeSocioAsync(Guid socioId);
    }

    /// <summary>
    /// Generación de la rutina del protocolo de piernas (Etapa 7d). Archivo y servicio propios: no hereda ni modifica
    /// PrescripcionBaselineService. Determinista (PlanificadorPiernas), sin IA, embeddings ni pgvector.
    ///
    /// La dosificación y las contraindicaciones por limitación son criterios de diseño provisionales, sin fuente
    /// bibliográfica ni validación clínica (decisión D5); los revisa la Personal Trainer antes de cualquier uso con
    /// personas reales.
    /// </summary>
    public class PrescripcionPiernasService : IPrescripcionPiernasService
    {
        private const int LongitudMaximaObservaciones = 500;

        public static readonly JsonSerializerOptions Json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
        };

        private readonly IProtocoloPiernasRepository _repo;
        private readonly ISocioAccessService _acceso;
        private readonly IRevisionPrescripcionService _revision;
        private readonly IAuditService _audit;
        private readonly IValidadorPrescripcionService _validador;
        private readonly IRegistroEventos _eventos;
        private readonly ProtocoloPiernasConfig _config;
        private readonly ReglasSeguridadConfig _reglas;
        private readonly TimeProvider _reloj;

        public PrescripcionPiernasService(IProtocoloPiernasRepository repo, ISocioAccessService acceso,
            IRevisionPrescripcionService revision, IAuditService audit, IValidadorPrescripcionService validador,
            IRegistroEventos eventos, ProtocoloPiernasConfig config, ReglasSeguridadConfig reglas, TimeProvider reloj)
        {
            _repo = repo;
            _acceso = acceso;
            _revision = revision;
            _audit = audit;
            _validador = validador;
            _eventos = eventos;
            _config = config;
            _reglas = reglas;
            _reloj = reloj;
        }

        public async Task<RutinaPendienteDto> GenerarAsync(Guid socioId)
        {
            // 1) Precondiciones, iguales a las del baseline.
            var socio = await _repo.GetSocioAsync(socioId)
                ?? throw new RecursoNoEncontradoException("Socio no encontrado.");
            await _acceso.AsegurarAccesoAsync(socioId);

            if (socio.ConsentimientoDatosSaludFecha is null)
                throw new ReglaNegocioException("El socio todavía no otorgó el consentimiento para el tratamiento de sus datos de salud.");

            var medicion = await _repo.GetUltimaMedicionAsync(socioId)
                ?? throw new ReglaNegocioException("El socio necesita al menos una medición física para generar la rutina.");

            if (await _repo.TienePendienteDeRevisionAsync(socioId))
                throw new ReglaNegocioException("El socio ya tiene una rutina pendiente de revisión.");

            // 2) Lecturas.
            var codigo = ProtocoloPiernasConfig.ProtocoloCodigo;
            var ahora = _reloj.GetUtcNow().UtcDateTime;
            var filas = await _repo.GetFilasProtocoloAsync(codigo, _config.Version);
            var factibles = socio.GimnasioId is Guid gimnasio ? await _repo.GetTiposEquipamientoFactiblesAsync(gimnasio) : null;
            var historial = await _repo.GetUltimosRegistrosAsync(socioId, filas.Select(f => f.EjercicioId).Distinct().ToList());
            var alertas = await _repo.GetAlertasVigentesAsync(socioId, ahora.AddDays(-_config.VentanaAlertasDias));
            var numero = await _repo.GetMaxNumeroGeneracionAsync(socioId, codigo) + 1;
            var edad = PrescripcionBaselineService.CalcularEdad(socio.FechaNacimiento, ahora);

            // 3) Planificación (función pura).
            var perfil = new PerfilPlanificacion(socioId, edad, socio.Nivel, socio.LimitacionRodilla, socio.LimitacionLumbar, numero,
                alertas.Count > 0, historial.ToDictionary(h => h.EjercicioId));
            var candidatos = filas.Select(f => new CandidatoProtocolo(f.EjercicioId, f.Nombre, f.Grupo, f.Rol, f.Clase,
                f.RodillaExcluirDesde, f.LumbarExcluirDesde, f.OrdenRotacion,
                factibles is null || (f.TipoEquipamientoId is Guid t && factibles.Contains(t)), f.TipoMedida)).ToList();
            var plan = PlanificadorPiernas.Planificar(perfil, candidatos, _config, _reglas);

            // 4) R-LIM: la rutina generada debe cumplir los límites duros; si no, no se persiste.
            var lineas = plan.Lineas.Select(l => new LineaPrescripcion(l.EjercicioId, l.Orden, l.Nombre, l.Series,
                l.RepeticionesMinimas, l.RepeticionesMaximas, l.TipoMedida, l.Rir, l.DescansoSegundos)).ToList();
            var violaciones = _validador.Validar(lineas, socio.Nivel);
            if (violaciones.Count > 0)
            {
                var codigos = string.Join(", ", violaciones.Select(v => v.Codigo).Distinct());
                _eventos.Advertencia($"Rutina de piernas rechazada por limites de seguridad: {codigos}.");
                throw new CatalogosIncompletosException(
                    $"La rutina de piernas generada no cumple los límites de seguridad configurados ({codigos}). " +
                    "Revise la configuración del protocolo.");
            }

            // 5) Prescripción con trazabilidad.
            var prescripcion = new PrescripcionEntrenamiento
            {
                SocioId = socioId,
                OrigenGeneracion = ProtocoloPiernasConfig.OrigenGeneracion,
                ProtocoloCodigo = codigo,
                ProtocoloVersion = _config.Version,
                NumeroGeneracion = numero,
                OrigenDatos = socio.OrigenDatos,
                FechaAlta = ahora,
            };
            foreach (var l in plan.Lineas)
            {
                prescripcion.EjerciciosPrescritos.Add(new PrescripcionEjercicio
                {
                    PrescripcionEntrenamientoId = prescripcion.Id,
                    EjercicioId = l.EjercicioId,
                    Orden = l.Orden,
                    SeriesObjetivo = l.Series,
                    RepeticionesMinimas = l.RepeticionesMinimas,
                    RepeticionesMaximas = l.RepeticionesMaximas,
                    RIRObjetivo = l.Rir,
                    DescansoObjetivoSegundos = l.DescansoSegundos,
                    Rol = l.Rol,
                    Grupo = l.Grupo,
                    TempoExcentricoSeg = l.TempoExcentricoSeg,
                    CargaSugeridaKg = l.CargaSugeridaKg,
                    MotivoSeleccion = JsonSerializer.Serialize(l.Motivo, Json),
                    FechaAlta = ahora,
                });
            }
            prescripcion.EntradaSnapshot = JsonSerializer.Serialize(new
            {
                protocolo = codigo,
                version = _config.Version,
                numeroGeneracion = numero,
                edad,
                nivel = socio.Nivel.ToString(),
                limitacionRodilla = socio.LimitacionRodilla.ToString(),
                limitacionLumbar = socio.LimitacionLumbar.ToString(),
                medicionId = medicion.Id,
                fcReposo = medicion.FrecuenciaCardiacaReposo,
                alertasVigentes = alertas,
                ventanaAlertasDias = _config.VentanaAlertasDias,
                equipamientoDeclarado = factibles is not null,
                historial = historial.OrderBy(h => h.EjercicioId).Select(h => new
                {
                    ejercicioId = h.EjercicioId,
                    sesionId = h.SesionId,
                    fecha = h.Inicio,
                    cargaMaximaKg = h.CargaMaximaKg,
                    adherenciaPorcentaje = h.AdherenciaPorcentaje,
                }).ToList(),
                configuracionSha256 = HuellaConfiguracion(_config),
            }, Json);
            prescripcion.Observaciones = ArmarObservaciones(plan, numero, factibles is not null);

            // 6) Estado inicial según la política de revisión, persistencia y auditoría (sin datos de salud en el detalle).
            await _revision.AplicarPoliticaAsync(prescripcion);
            await _repo.AddPrescripcionAsync(prescripcion);
            await _repo.SaveChangesAsync();
            await _audit.RegistrarAsync("GENERACION_PIERNAS", nameof(PrescripcionEntrenamiento),
                prescripcion.Id.ToString(), prescripcion.Estado.ToString());

            var guardada = (await _repo.GetPrescripcionesDeSocioAsync(socioId)).First(p => p.Id == prescripcion.Id);
            return RutinaMapeo.Mapear(guardada);
        }

        public async Task<IEnumerable<RutinaPendienteDto>> ListarDeSocioAsync(Guid socioId)
        {
            await _acceso.AsegurarAccesoAsync(socioId);
            return (await _repo.GetPrescripcionesDeSocioAsync(socioId)).Select(RutinaMapeo.Mapear).ToList();
        }

        /// <summary>
        /// SHA-256 (hexadecimal) del JSON canónico de la configuración (claves ordenadas, sin espacios), con la composición
        /// efectiva. Identifica los parámetros exactos con los que se generó una rutina.
        /// </summary>
        public static string HuellaConfiguracion(ProtocoloPiernasConfig config)
        {
            var nodo = JsonSerializer.SerializeToNode(config, Json)!.AsObject();
            nodo["composicion"] = JsonSerializer.SerializeToNode(config.ComposicionEfectiva(), Json);
            var canonico = ReglasSeguridadService.JsonCanonico(nodo.ToJsonString());
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonico))).ToLowerInvariant();
        }

        private string ArmarObservaciones(ResultadoPlanificacion plan, int numero, bool equipamientoDeclarado)
        {
            var partes = new List<string>
            {
                $"Rutina de piernas determinista (protocolo {_config.Version}, generación {numero}; sin IA): " +
                $"{plan.Lineas.Count} ejercicios, {plan.SeriesTotales} series.",
                "Parámetros provisionales, sin validación clínica: los revisa la Personal Trainer.",
            };
            if (plan.Omitidas.Count > 0)
                partes.Add("Posiciones omitidas: " + string.Join(", ", plan.Omitidas.Select(o =>
                    $"{o.Posicion} ({PlanificadorPiernas.NombreGrupo(Enum.Parse<GrupoMuscularPiernas>(o.Grupo))}, {o.Rol})")) + ".");
            if (plan.ReduccionPorAlerta)
                partes.Add("Volumen reducido por alerta de seguridad vigente.");
            if (equipamientoDeclarado)
                partes.Add("Se usó el equipamiento declarado por el gimnasio.");

            var texto = string.Join(" ", partes);
            return texto.Length <= LongitudMaximaObservaciones ? texto : texto[..(LongitudMaximaObservaciones - 1)] + "…";
        }
    }

    /// <summary>Mapeo de una prescripción al DTO de rutina, con los campos aditivos de la Etapa 7d.</summary>
    public static class RutinaMapeo
    {
        public static RutinaPendienteDto Mapear(PrescripcionEntrenamiento p) => new()
        {
            Id = p.Id,
            SocioId = p.SocioId,
            NombreSocio = p.Socio?.NombreCompleto ?? string.Empty,
            OrigenGeneracion = p.OrigenGeneracion,
            Observaciones = p.Observaciones,
            Estado = p.Estado.ToString(),
            FechaAlta = p.FechaAlta,
            ProtocoloVersion = p.ProtocoloVersion,
            NumeroGeneracion = p.NumeroGeneracion,
            Ejercicios = p.EjerciciosPrescritos.OrderBy(e => e.Orden).Select(e => new RutinaEjercicioDto
            {
                Id = e.Id,
                EjercicioId = e.EjercicioId,
                Orden = e.Orden,
                Ejercicio = e.Ejercicio?.Nombre ?? string.Empty,
                SeriesObjetivo = e.SeriesObjetivo,
                RepeticionesMinimas = e.RepeticionesMinimas,
                RepeticionesMaximas = e.RepeticionesMaximas,
                TipoMedida = (e.Ejercicio?.TipoMedida ?? TipoMedida.Repeticiones).ToString(),
                RIRObjetivo = e.RIRObjetivo,
                DescansoObjetivoSegundos = e.DescansoObjetivoSegundos,
                Observaciones = e.Observaciones,
                Rol = e.Rol?.ToString(),
                Grupo = e.Grupo?.ToString(),
                TempoExcentricoSeg = e.TempoExcentricoSeg,
                CargaSugeridaKg = e.CargaSugeridaKg,
            }).ToList()
        };
    }
}
