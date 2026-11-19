using GymBro.Application.Abstractions;
using GymBro.Application.DTOs.Acceso;
using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Domain.Exceptions;
using GymBro.Domain.Repositories;
using GymBro.Domain.Rules;

namespace GymBro.Application.Services
{
    /// <summary>Zona anatómica lógica del baseline y los nombres con los que figura en el catálogo.</summary>
    public class ZonaBaseline
    {
        public string Nombre { get; set; } = string.Empty;
        /// <summary>Valores de <c>ZonaObjetivo</c> del catálogo que corresponden a la zona (sin distinguir mayúsculas).</summary>
        public List<string> Alias { get; set; } = new();
    }

    /// <summary>
    /// Configuración del baseline (sección "Baseline" de appsettings). Si no se define "Zonas" se usan las
    /// cinco zonas del Análisis de Fase 2. Los alias deben coincidir con los valores reales de
    /// <c>ejercicios.zona_objetivo</c>; se consultan con GET /api/Ejercicios/zonas.
    /// Valores reales verificados el 16/11/2026: Chest, Back, Legs, Arms, Forearms, Shoulders y Movilidad.
    /// El catálogo no tiene ejercicios de Core/Abs, por lo que esa zona se omite siempre hasta cargarlos.
    /// Para sumar Hombros: Zonas = [..., { Nombre: "Hombros", Alias: ["Shoulders"] }] en appsettings.
    /// </summary>
    public class BaselineConfig
    {
        public List<ZonaBaseline> Zonas { get; set; } = new();
        public int RirPorDefecto { get; set; } = 2;
        public int DescansoPorDefectoSegundos { get; set; } = 90;

        public IReadOnlyList<ZonaBaseline> ZonasEfectivas() => Zonas.Count > 0 ? Zonas : ZonasPorDefecto;

        public static readonly IReadOnlyList<ZonaBaseline> ZonasPorDefecto = new List<ZonaBaseline>
        {
            new() { Nombre = "Pecho",   Alias = new() { "Chest", "Pecho" } },
            new() { Nombre = "Espalda", Alias = new() { "Back", "Espalda" } },
            new() { Nombre = "Piernas", Alias = new() { "Legs", "Piernas" } },
            new() { Nombre = "Brazos",  Alias = new() { "Arms", "Brazos" } },
            new() { Nombre = "Core",    Alias = new() { "Core", "Abs", "Abdominales" } },
        };
    }

    /// <summary>El catálogo no tiene datos suficientes para generar la rutina (HTTP 500: es un problema de datos del sistema).</summary>
    public class CatalogosIncompletosException : Exception
    {
        public CatalogosIncompletosException(string mensaje) : base(mensaje) { }
    }

    public interface IPrescripcionBaselineService
    {
        /// <summary>Genera la rutina baseline determinista del socio y la deja en revisión o aprobada según la política del gimnasio.</summary>
        Task<RutinaPendienteDto> GenerarAsync(Guid socioId);

        /// <summary>Rutinas del socio (todas las del historial), la más reciente primero.</summary>
        Task<IEnumerable<RutinaPendienteDto>> ListarDeSocioAsync(Guid socioId);
    }

    public class PrescripcionBaselineService : IPrescripcionBaselineService
    {
        public const string Origen = "BASELINE_DETERMINISTA";
        private const int LongitudMaximaObservaciones = 500;

        // Dosis usada solo cuando la dosificación del catálogo no tiene una fila válida para la zona y el tipo de medida.
        // Repeticiones: 3 x 8-12. Segundos (Etapa 9a-1, propuesta de diseño, no criterio clínico): 3 x 20-40 s.
        private const int SeriesPorDefecto = 3, RepMinPorDefecto = 8, RepMaxPorDefecto = 12;
        private const int SegMinPorDefecto = 20, SegMaxPorDefecto = 40;

        /// <summary>Dosis por defecto según el tipo de medida: (series, mínimo, máximo, unidad para el texto).</summary>
        public static (int Series, int Min, int Max, string Unidad) DosisPorDefecto(TipoMedida tipo) =>
            tipo == TipoMedida.Segundos
                ? (SeriesPorDefecto, SegMinPorDefecto, SegMaxPorDefecto, "s")
                : (SeriesPorDefecto, RepMinPorDefecto, RepMaxPorDefecto, "repeticiones");

        private readonly IBaselineRepository _repo;
        private readonly ISocioAccessService _acceso;
        private readonly IRevisionPrescripcionService _revision;
        private readonly IAuditService _audit;
        private readonly BaselineConfig _config;

        private readonly IValidadorPrescripcionService _validador;
        private readonly IRegistroEventos _eventos;

        public PrescripcionBaselineService(IBaselineRepository repo, ISocioAccessService acceso,
            IRevisionPrescripcionService revision, IAuditService audit, BaselineConfig config,
            IValidadorPrescripcionService validador, IRegistroEventos eventos)
        {
            _validador = validador;
            _eventos = eventos;
            _repo = repo;
            _acceso = acceso;
            _revision = revision;
            _audit = audit;
            _config = config;
        }

        public async Task<RutinaPendienteDto> GenerarAsync(Guid socioId)
        {
            // Si el socio no existe o no es visible para el usuario, se responde 404 (el filtro de tenant lo oculta).
            var socio = await _repo.GetSocioAsync(socioId)
                ?? throw new RecursoNoEncontradoException("Socio no encontrado.");
            await _acceso.AsegurarAccesoAsync(socioId);

            if (socio.ConsentimientoDatosSaludFecha is null)
                throw new ReglaNegocioException("El socio todavía no otorgó el consentimiento para el tratamiento de sus datos de salud.");

            var medicion = await _repo.GetUltimaMedicionAsync(socioId)
                ?? throw new ReglaNegocioException("El socio necesita al menos una medición física para generar la rutina.");

            if (await _repo.TienePendienteDeRevisionAsync(socioId))
                throw new ReglaNegocioException("El socio ya tiene una rutina pendiente de revisión.");

            if (!await _repo.HayDosificacionesAsync())
                throw new CatalogosIncompletosException("La tabla de dosificación volumétrica está vacía.");

            // 1) Perfil del socio.
            var edad = CalcularEdad(socio.FechaNacimiento, DateTime.UtcNow);
            // Las mediciones cargadas desde la app traen solo peso y altura (IMC = 0): en ese caso se calcula.
            var imc = medicion.IMC > 0 ? medicion.IMC : ClasificadorIMC.Calcular(medicion.PesoKg, medicion.AlturaCm)
                ?? throw new ReglaNegocioException("La última medición no tiene IMC ni peso y altura para calcularlo.");
            var categoriaImc = ClasificadorIMC.Clasificar(imc);
            var genero = string.IsNullOrWhiteSpace(socio.Genero) ? "O" : socio.Genero.Trim()[..1].ToUpperInvariant();

            // 2) Matriz de recomendaciones (consulta determinista, no un motor de reglas).
            var matriz = await _repo.BuscarMatrizAsync(
                genero, edad, medicion.TieneHipertension, medicion.TieneDiabetes, categoriaImc, socio.Objetivo);
            var preferencias = EquipamientoPreferido(matriz?.CategoriaRutina);

            // 3 y 4) Un ejercicio por zona, con el volumen de la zona.
            var prescripcion = new PrescripcionEntrenamiento
            {
                SocioId = socioId,
                OrigenGeneracion = Origen,
            };
            var omitidas = new List<string>();
            var omitidasPorEquipamiento = new List<string>();
            var orden = 0;
            var tiposMedida = new Dictionary<Guid, TipoMedida>();   // Etapa 10a: para validar segundos o repeticiones

            // Etapa 9a-2: se usa el gimnasio del socio (no el de quien llama). Si el gimnasio declaró equipamiento, solo
            // son factibles los ejercicios cuyo tipo está marcado o es siempre disponible; si no declaró nada, o el socio
            // es independiente, no se filtra por equipamiento.
            var factibles = socio.GimnasioId is Guid gimnasioSocio
                ? await _repo.GetTiposEquipamientoFactiblesAsync(gimnasioSocio)
                : null;

            foreach (var zona in _config.ZonasEfectivas())
            {
                var deLaZona = await _repo.GetEjerciciosDeZonaAsync(zona.Alias, socio.GimnasioId);
                if (deLaZona.Count == 0) { omitidas.Add(zona.Nombre); continue; }

                var candidatos = factibles is null
                    ? deLaZona
                    : deLaZona.Where(c => c.TipoEquipamientoId is Guid t && factibles.Contains(t)).ToList();
                if (candidatos.Count == 0) { omitidasPorEquipamiento.Add(zona.Nombre); continue; }

                var elegido = candidatos.FirstOrDefault(c => preferencias.Any(p =>
                                  c.Equipamiento.Contains(p, StringComparison.OrdinalIgnoreCase)))
                              ?? candidatos[0];

                // La dosis se elige según el tipo de medida del ejercicio elegido (Etapa 9a-1).
                var dosis = await _repo.BuscarDosificacionAsync(zona.Alias, elegido.TipoMedida);
                var dosisValida = dosis is { SeriesMinimas: > 0, RepeticionesMinimas: > 0 }
                                  && dosis.RepeticionesMaximas >= dosis.RepeticionesMinimas;
                var porDefecto = DosisPorDefecto(elegido.TipoMedida);

                orden++;
                tiposMedida[elegido.Id] = elegido.TipoMedida;
                prescripcion.EjerciciosPrescritos.Add(new PrescripcionEjercicio
                {
                    PrescripcionEntrenamientoId = prescripcion.Id,
                    EjercicioId = elegido.Id,
                    Orden = orden,
                    SeriesObjetivo = dosisValida ? dosis!.SeriesMinimas : porDefecto.Series,
                    RepeticionesMinimas = dosisValida ? dosis!.RepeticionesMinimas : porDefecto.Min,
                    RepeticionesMaximas = dosisValida ? dosis!.RepeticionesMaximas : porDefecto.Max,
                    RIRObjetivo = _config.RirPorDefecto,
                    DescansoObjetivoSegundos = _config.DescansoPorDefectoSegundos,
                    Observaciones = dosisValida
                        ? null
                        : $"Sin dosificación en el catálogo para la zona {zona.Nombre} ({elegido.TipoMedida}): " +
                          $"se usó {porDefecto.Series} x {porDefecto.Min}-{porDefecto.Max} {porDefecto.Unidad}.",
                });
            }

            if (prescripcion.EjerciciosPrescritos.Count == 0 && omitidasPorEquipamiento.Count > 0)
                throw new ReglaNegocioException(
                    "Con el equipamiento declarado por el gimnasio no hay ejercicios factibles para ninguna zona del baseline.");
            if (prescripcion.EjerciciosPrescritos.Count == 0)
                throw new CatalogosIncompletosException("El catálogo no tiene ejercicios para ninguna de las zonas del baseline.");

            // Etapa 10a (R-LIM, decisión P4): el baseline debe cumplir siempre los límites duros. Si no los cumple es un
            // problema de configuración o de datos del catálogo: no se genera (para que no quede aprobada sola en un socio
            // independiente). En el log van solo los códigos, sin datos de salud.
            var lineas = prescripcion.EjerciciosPrescritos.Select(e => new LineaPrescripcion(e.EjercicioId, e.Orden, null,
                e.SeriesObjetivo, e.RepeticionesMinimas, e.RepeticionesMaximas, tiposMedida[e.EjercicioId], e.RIRObjetivo,
                e.DescansoObjetivoSegundos)).ToList();
            var violaciones = _validador.Validar(lineas, socio.Nivel);
            if (violaciones.Count > 0)
            {
                var codigos = string.Join(", ", violaciones.Select(v => v.Codigo).Distinct());
                _eventos.Advertencia($"Baseline rechazado por limites de seguridad: {codigos}.");
                throw new CatalogosIncompletosException(
                    $"La rutina generada no cumple los límites de seguridad configurados ({codigos}). " +
                    "Revise la configuración del baseline y la dosificación del catálogo.");
            }

            prescripcion.Observaciones = ArmarObservaciones(edad, genero, categoriaImc, medicion.TieneHipertension,
                medicion.TieneDiabetes, socio.Objetivo.ToString(), matriz, omitidas, omitidasPorEquipamiento,
                factibles is not null, prescripcion.EjerciciosPrescritos.Count);

            // 5) Estado inicial según la política de revisión del gimnasio (independiente: aprobada).
            await _revision.AplicarPoliticaAsync(prescripcion);

            await _repo.AddPrescripcionAsync(prescripcion);
            await _repo.SaveChangesAsync();
            await _audit.RegistrarAsync("GENERACION_BASELINE", nameof(PrescripcionEntrenamiento),
                prescripcion.Id.ToString(), prescripcion.Estado.ToString());

            // Se vuelve a leer para devolver los nombres de los ejercicios.
            var guardada = (await _repo.GetPrescripcionesDeSocioAsync(socioId)).First(p => p.Id == prescripcion.Id);
            return Mapear(guardada);
        }

        public async Task<IEnumerable<RutinaPendienteDto>> ListarDeSocioAsync(Guid socioId)
        {
            await _acceso.AsegurarAccesoAsync(socioId);
            return (await _repo.GetPrescripcionesDeSocioAsync(socioId)).Select(Mapear).ToList();
        }

        public static int CalcularEdad(DateTime nacimiento, DateTime hoy)
        {
            var edad = hoy.Year - nacimiento.Year;
            if (nacimiento.Date > hoy.Date.AddYears(-edad)) edad--;
            return edad;
        }

        /// <summary>
        /// Heurística simple: si la categoría de rutina de la matriz habla de mancuernas, se prefieren ejercicios con
        /// ese equipamiento. Es una preferencia: si la zona no tiene ejercicios con él, se toma el primero disponible.
        /// </summary>
        private static string[] EquipamientoPreferido(string? categoriaRutina)
        {
            if (string.IsNullOrWhiteSpace(categoriaRutina)) return Array.Empty<string>();
            return categoriaRutina.Contains("mancuerna", StringComparison.OrdinalIgnoreCase)
                   || categoriaRutina.Contains("dumbbell", StringComparison.OrdinalIgnoreCase)
                ? new[] { "dumbbell", "mancuerna" }
                : Array.Empty<string>();
        }

        private static string ArmarObservaciones(int edad, string genero, GymBro.Domain.Enums.CategoriaIMC imc,
            bool hta, bool dm, string objetivo, MatrizRecomendacion? matriz, List<string> omitidas,
            List<string> omitidasPorEquipamiento, bool filtroEquipamiento, int cantidad)
        {
            var partes = new List<string>
            {
                $"Rutina baseline determinista (sin IA). Perfil: {edad} años, género {genero}, IMC {imc}, objetivo {objetivo}, " +
                $"hipertensión {(hta ? "sí" : "no")}, diabetes {(dm ? "sí" : "no")}."
            };
            if (omitidas.Count > 0)
                partes.Add($"Zonas sin ejercicios en el catálogo, omitidas: {string.Join(", ", omitidas)} (la rutina quedó con {cantidad}).");
            // Etapa 9a-2: se distingue la zona omitida por falta de ejercicios de la omitida por el equipamiento declarado.
            if (omitidasPorEquipamiento.Count > 0)
                partes.Add($"Zonas omitidas por equipamiento (el gimnasio no declaró el necesario): {string.Join(", ", omitidasPorEquipamiento)} (la rutina quedó con {cantidad}).");
            else if (filtroEquipamiento)
                partes.Add("Se usó el equipamiento declarado por el gimnasio.");
            if (matriz is null)
                partes.Add("La matriz de recomendaciones no tiene filas: sin sugerencias adicionales.");
            else
            {
                if (!string.IsNullOrWhiteSpace(matriz.CategoriaRutina)) partes.Add($"Categoría de rutina: {matriz.CategoriaRutina}.");
                if (!string.IsNullOrWhiteSpace(matriz.RecomendacionGeneral)) partes.Add($"Recomendación: {matriz.RecomendacionGeneral}.");
                if (!string.IsNullOrWhiteSpace(matriz.SugerenciaDieta)) partes.Add($"Dieta sugerida: {matriz.SugerenciaDieta}.");
            }

            var texto = string.Join(" ", partes);
            return texto.Length <= LongitudMaximaObservaciones ? texto : texto[..(LongitudMaximaObservaciones - 1)] + "…";
        }

        private static RutinaPendienteDto Mapear(PrescripcionEntrenamiento p) => new()
        {
            Id = p.Id,
            SocioId = p.SocioId,
            NombreSocio = p.Socio?.NombreCompleto ?? string.Empty,
            OrigenGeneracion = p.OrigenGeneracion,
            Observaciones = p.Observaciones,
            Estado = p.Estado.ToString(),
            FechaAlta = p.FechaAlta,
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
                Observaciones = e.Observaciones
            }).ToList()
        };
    }
}
