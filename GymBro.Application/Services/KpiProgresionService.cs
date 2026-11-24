using System.Text.Json;
using System.Text.Json.Nodes;
using GymBro.Application.DTOs;
using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Domain.Exceptions;
using GymBro.Domain.Repositories;
using GymBro.Domain.Rules;
using GymBro.Domain.Rules.Kpis;

namespace GymBro.Application.Services
{
    public interface IKpiProgresionService
    {
        /// <summary>
        /// K1 a K4 del socio en el período (extremos opcionales, sobre el inicio de la sesión). Solo socios Sintetico;
        /// con <paramref name="incluirValidacionFuncional"/> también el perfil ValidacionFuncional (para verlo en pantalla,
        /// nunca para el estudio).
        /// </summary>
        Task<KpiProgresionDto> CalcularAsync(Guid socioId, DateTime? desde, DateTime? hasta, bool incluirValidacionFuncional);
    }

    /// <summary>
    /// Lectura de los KPIs de progresión (Etapa 7d, D6). Arma las entradas y aplica las funciones puras de
    /// <see cref="KpisProgresion"/>. Indicadores descriptivos, no predictivos ni causales; con datos sintéticos K1 y K2
    /// reflejan el modelo del generador. Las alertas con baja lógica no cuentan (puede subestimar K4 en períodos pasados).
    /// Mismo acceso que los demás datos de salud del socio: el Administrador de la Plataforma no accede.
    /// </summary>
    public class KpiProgresionService : IKpiProgresionService
    {
        private readonly IKpiProgresionRepository _repo;
        private readonly ISocioAccessService _acceso;

        public KpiProgresionService(IKpiProgresionRepository repo, ISocioAccessService acceso)
        {
            _repo = repo;
            _acceso = acceso;
        }

        public async Task<KpiProgresionDto> CalcularAsync(Guid socioId, DateTime? desde, DateTime? hasta, bool incluirValidacionFuncional)
        {
            await _acceso.AsegurarAccesoAsync(socioId);
            var socio = await _repo.GetSocioAsync(socioId) ?? throw new RecursoNoEncontradoException("Socio no encontrado.");
            if (desde is DateTime d0 && hasta is DateTime h0 && d0 > h0)
                throw new ReglaNegocioException("La fecha desde no puede ser posterior a la fecha hasta.");

            var dto = new KpiProgresionDto
            {
                SocioId = socioId,
                OrigenDatos = socio.OrigenDatos.ToString(),
                IncluyeValidacionFuncional = incluirValidacionFuncional,
                Desde = desde,
                Hasta = hasta,
            };
            var incluido = socio.OrigenDatos == OrigenDatos.Sintetico
                           || (incluirValidacionFuncional && socio.OrigenDatos == OrigenDatos.ValidacionFuncional);
            if (!incluido)
            {
                dto.Motivo = $"El socio tiene origen de datos {socio.OrigenDatos}: los KPIs del estudio incluyen solo datos sintéticos.";
                return dto;
            }
            dto.Incluido = true;

            var todas = await _repo.GetSesionesAsync(socioId, desde, hasta);
            var sesiones = todas.Where(s => s.PrescripcionEntrenamiento?.ExcluidaEstudio != true).ToList();
            dto.SesionesExcluidas = todas.Count - sesiones.Count;

            var grupos = await _repo.GetGruposProtocoloAsync(ProtocoloPiernasConfig.ProtocoloCodigo);
            var alertas = await _repo.GetAlertasVigentesAsync(socioId);
            var bienestar = await _repo.GetFechasBienestarAsync(socioId);
            var entradas = sesiones.Select(s => ArmarSesion(s, grupos, alertas, bienestar)).ToList();

            var generaciones = (await _repo.GetPrescripcionesProtocoloAsync(socioId, ProtocoloPiernasConfig.ProtocoloCodigo, desde, hasta))
                .Where(p => !p.ExcluidaEstudio)
                .Count(p => p.EjerciciosPrescritos.Any(l => ConReduccion(l.MotivoSeleccion)));

            dto.K1DeltaCargaMaxima = KpisProgresion.K1DeltaCargaMaxima(entradas);
            dto.K2VolumenEfectivo = KpisProgresion.K2VolumenEfectivo(entradas);
            dto.K3Adherencia = KpisProgresion.K3Adherencia(entradas);
            dto.K4IndiceSeguridad = KpisProgresion.K4IndiceSeguridad(entradas, generaciones);
            return dto;
        }

        /// <summary>Entradas de una sesión: series sin baja, línea vinculada (directa o por ejercicio y orden) y alertas.</summary>
        public static SesionKpi ArmarSesion(SesionEntrenamiento s, IReadOnlyDictionary<Guid, GrupoMuscularPiernas> grupos,
            IReadOnlyList<AlertaSeguridad> alertas, IReadOnlyDictionary<Guid, DateOnly> bienestar)
        {
            var lineas = s.PrescripcionEntrenamiento?.EjerciciosPrescritos.Where(l => l.FechaBaja == null)
                .OrderBy(l => l.Orden).ToList() ?? new List<PrescripcionEjercicio>();
            LineaKpi Linea(PrescripcionEjercicio l) =>
                new(l.Id, l.EjercicioId, l.SeriesObjetivo, l.RepeticionesMinimas, l.RepeticionesMaximas, l.RIRObjetivo);

            var ejercicios = s.EjerciciosEjecutados.Where(e => e.FechaBaja == null).OrderBy(e => e.Orden).ThenBy(e => e.Id).Select(e =>
            {
                var linea = e.PrescripcionEjercicioId is Guid id
                    ? lineas.FirstOrDefault(l => l.Id == id)
                    : lineas.FirstOrDefault(l => l.EjercicioId == e.EjercicioId && l.Orden == e.Orden)
                      ?? (lineas.Count(l => l.EjercicioId == e.EjercicioId) == 1 ? lineas.First(l => l.EjercicioId == e.EjercicioId) : null);
                return new EjercicioSesionKpi(e.EjercicioId, e.Ejercicio?.TipoMedida ?? TipoMedida.Repeticiones,
                    grupos.TryGetValue(e.EjercicioId, out var g) ? g : null, linea is null ? null : Linea(linea),
                    e.SeriesRealizadas.Where(x => x.FechaBaja == null).OrderBy(x => x.OrdenSerie)
                        .Select(x => new SerieKpi(x.PesoLevantadoKg, x.Repeticiones, x.RIR, x.EsCalentamiento)).ToList());
            }).ToList();

            var dia = DiaLocal.DeUtc(s.Inicio);
            var alertaAsociada = alertas.Any(a => a.Tipo == TipoAlerta.Alerta && a.FechaBaja == null && (
                a.SesionId == s.Id
                || (a.ReglaCodigo == CodigosRegla.FcReposo && a.RegistroBienestarId is Guid r
                    && bienestar.TryGetValue(r, out var fecha) && fecha == dia)));
            var bajarCarga = alertas.Any(a => a.ReglaCodigo == CodigosRegla.Progresion && a.FechaBaja == null && a.SesionId == s.Id
                                              && Direccion(a.Detalle) == nameof(DireccionCarga.Bajar));

            return new SesionKpi(s.Id, s.Inicio, s.PrescripcionEntrenamientoId, ejercicios, lineas.Select(Linea).ToList(),
                alertaAsociada, bajarCarga);
        }

        private static string? Direccion(string detalle)
        {
            try { return JsonNode.Parse(detalle)?["resultado"]?["direccion"]?.GetValue<string>(); }
            catch (JsonException) { return null; }
            catch (InvalidOperationException) { return null; }
        }

        private static bool ConReduccion(string? motivo)
        {
            if (string.IsNullOrWhiteSpace(motivo)) return false;
            try { return JsonNode.Parse(motivo)?["reduccionPorAlerta"]?.GetValue<bool>() == true; }
            catch (JsonException) { return false; }
            catch (InvalidOperationException) { return false; }
        }
    }
}
