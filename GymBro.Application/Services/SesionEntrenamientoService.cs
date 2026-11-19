using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GymBro.Application.Abstractions;
using GymBro.Application.DTOs;
using GymBro.Application.DTOs.Comparacion;
using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Domain.Exceptions;
using GymBro.Domain.Repositories;

namespace GymBro.Application.Services
{
    public class SesionEntrenamientoService : ISesionEntrenamientoService
    {
        private readonly ISesionEntrenamientoRepository _sesionRepo;
        private readonly ISocioRepository _socioRepo;
        private readonly IEjercicioRepository _ejercicioRepo;
        private readonly IPrescripcionEntrenamientoRepository _prescripcionRepo;
        private readonly ISocioAccessService _acceso;
        private readonly IReglasSeguridadService _reglas;
        private readonly IRegistroEventos _eventos;

        public SesionEntrenamientoService(
            ISesionEntrenamientoRepository sesionRepo,
            ISocioRepository socioRepo,
            IEjercicioRepository ejercicioRepo,
            IPrescripcionEntrenamientoRepository prescripcionRepo,
            ISocioAccessService acceso,
            IReglasSeguridadService reglas,
            IRegistroEventos eventos)
        {
            _acceso = acceso;
            _reglas = reglas;
            _eventos = eventos;
            _sesionRepo = sesionRepo;
            _socioRepo = socioRepo;
            _ejercicioRepo = ejercicioRepo;
            _prescripcionRepo = prescripcionRepo;
        }

        public async Task<SesionEntrenamientoResponseDto> RegistrarSesionAsync(RegistrarSesionEntrenamientoRequest request)
        {
            await _acceso.AsegurarAccesoAsync(request.SocioId);
            if (!await _socioRepo.ExistsAsync(request.SocioId))
                throw new ArgumentException("El SocioId especificado no existe.");

            if (request.PrescripcionEntrenamientoId.HasValue)
            {
                // El filtro de tenant limita la búsqueda al gimnasio del usuario; acá se exige además que la
                // prescripción sea del mismo socio y que esté vigente (Aprobada o Editada).
                var prescripcion = await _prescripcionRepo.GetByIdAsync(request.PrescripcionEntrenamientoId.Value)
                    ?? throw new ArgumentException("La PrescripcionEntrenamientoId especificada no existe.");
                if (prescripcion.SocioId != request.SocioId)
                    throw new ReglaNegocioException("La prescripción indicada no pertenece al socio de la sesión.");
                if (!prescripcion.EsVigente())
                    throw new ReglaNegocioException(
                        $"La prescripción no está vigente (estado {prescripcion.Estado}): solo se registran sesiones de rutinas Aprobadas o Editadas.");
            }

            var ejercicioIds = request.Ejercicios.Select(e => e.EjercicioId).Distinct().ToList();
            if (ejercicioIds.Any())
            {
                if (!await _ejercicioRepo.ExistenTodosAsync(ejercicioIds))
                    throw new ArgumentException("Uno o más EjercicioId no existen en el catálogo.");
            }

            foreach (var ej in request.Ejercicios)
            {
                foreach (var serie in ej.SeriesRealizadas)
                {
                    if (serie.OrdenSerie <= 0) throw new ArgumentException("El OrdenSerie debe ser mayor a 0.");
                    if (serie.Repeticiones <= 0) throw new ArgumentException("Las repeticiones deben ser mayores a 0.");
                    if (serie.PesoLevantadoKg < 0) throw new ArgumentException("El peso levantado no puede ser negativo.");
                    if (serie.RIR.HasValue && (serie.RIR < 0 || serie.RIR > 5)) throw new ArgumentException("El RIR debe estar entre 0 y 5.");
                    if (serie.TiempoDescansoSegundos.HasValue && serie.TiempoDescansoSegundos < 0) throw new ArgumentException("El tiempo de descanso no puede ser negativo.");
                }
            }

            var nuevaSesion = new SesionEntrenamiento
            {
                Id = Guid.NewGuid(),
                SocioId = request.SocioId,
                PrescripcionEntrenamientoId = request.PrescripcionEntrenamientoId,
                Inicio = request.Inicio,
                Fin = request.Fin,
                CaloriasQuemadas = request.CaloriasQuemadas ?? 0,
                FrecuenciaCardiacaMaxima = request.FrecuenciaCardiacaMaxima ?? 0,
                FrecuenciaCardiacaPromedio = request.FrecuenciaCardiacaPromedio ?? 0,
                EjerciciosEjecutados = request.Ejercicios.Select(ej => new SesionEjercicio
                {
                    Id = Guid.NewGuid(),
                    EjercicioId = ej.EjercicioId,
                    Orden = ej.Orden,
                    Observaciones = ej.Observaciones,
                    SeriesRealizadas = ej.SeriesRealizadas.Select(s => new SerieRealizada
                    {
                        Id = Guid.NewGuid(),
                        OrdenSerie = s.OrdenSerie,
                        PesoLevantadoKg = s.PesoLevantadoKg,
                        Repeticiones = s.Repeticiones,
                        RIR = s.RIR,
                        EsCalentamiento = s.EsCalentamiento,
                        TiempoDescansoSegundos = s.TiempoDescansoSegundos
                    }).ToList()
                }).ToList()
            };

            await _sesionRepo.AddAsync(nuevaSesion);

            // Etapa 10a: reglas de seguridad (R-RIR-1 y R-PROG-1), síncronas y tolerantes a fallos (decisión D6):
            // un error se registra (sin datos de salud) y la sesión queda guardada igual.
            try
            {
                await _reglas.EvaluarSesionAsync(nuevaSesion.Id);
            }
            catch (Exception ex)
            {
                _eventos.Error($"Fallo la evaluacion de reglas de seguridad de la sesion {nuevaSesion.Id}.", ex);
            }

            return await GetByIdAsync(nuevaSesion.Id) ?? throw new InvalidOperationException("Error al recuperar la sesión tras ser creada.");
        }

        public async Task<SesionEntrenamientoResponseDto?> GetByIdAsync(Guid id)
        {
            var sesion = await _sesionRepo.GetByIdAsync(id);
            if (sesion == null) return null;
            await _acceso.AsegurarAccesoAsync(sesion.SocioId);

            return MapToResponseDto(sesion);
        }

        public async Task<IEnumerable<SesionEntrenamientoResponseDto>> GetAllBySocioIdAsync(Guid socioId)
        {
            await _acceso.AsegurarAccesoAsync(socioId);
            var sesiones = await _sesionRepo.GetAllBySocioIdAsync(socioId);
            return sesiones.Select(MapToResponseDto);
        }

        public async Task<ComparacionSesionResponseDto?> GetComparacionAsync(Guid sesionId)
        {
            var sesion = await _sesionRepo.GetByIdAsync(sesionId);
            if (sesion == null) return null;
            await _acceso.AsegurarAccesoAsync(sesion.SocioId);

            PrescripcionEntrenamiento? prescripcion = null;
            if (sesion.PrescripcionEntrenamientoId.HasValue)
            {
                prescripcion = await _prescripcionRepo.GetByIdAsync(sesion.PrescripcionEntrenamientoId.Value);
            }

            var comparacionDto = new ComparacionSesionResponseDto
            {
                SesionId = sesion.Id,
                PrescripcionEntrenamientoId = sesion.PrescripcionEntrenamientoId,
                Ejercicios = new List<EjercicioComparadoDto>()
            };

            var ejerciciosPrescritos = prescripcion?.EjerciciosPrescritos.ToList() ?? new List<PrescripcionEjercicio>();
            var ejerciciosEjecutados = sesion.EjerciciosEjecutados.ToList();

            var allKeys = ejerciciosPrescritos.Select(p => new { p.EjercicioId, p.Orden })
                .Union(ejerciciosEjecutados.Select(e => new { e.EjercicioId, e.Orden }))
                .Distinct()
                .ToList();

            foreach (var key in allKeys)
            {
                var pres = ejerciciosPrescritos.FirstOrDefault(p => p.EjercicioId == key.EjercicioId && p.Orden == key.Orden);
                var ejec = ejerciciosEjecutados.FirstOrDefault(e => e.EjercicioId == key.EjercicioId && e.Orden == key.Orden);

                // Etapa 9a-1: la unidad sale del ejercicio. Con Segundos no se multiplica kg por segundos (volumen 0).
                var tipoMedida = (ejec?.Ejercicio ?? pres?.Ejercicio)?.TipoMedida ?? TipoMedida.Repeticiones;
                var enSegundos = tipoMedida == TipoMedida.Segundos;
                decimal Volumen(SerieRealizada s) => enSegundos ? 0m : s.PesoLevantadoKg * s.Repeticiones;

                var dto = new EjercicioComparadoDto
                {
                    EjercicioId = key.EjercicioId,
                    Orden = key.Orden,
                    TipoMedida = tipoMedida.ToString(),
                    Comparacion = new ComparacionGeneralDto()
                };

                if (pres != null)
                {
                    dto.Prescripcion = new PrescripcionResumenDto
                    {
                        SeriesObjetivo = pres.SeriesObjetivo,
                        RepeticionesMinimas = pres.RepeticionesMinimas,
                        RepeticionesMaximas = pres.RepeticionesMaximas,
                        RIRObjetivo = pres.RIRObjetivo,
                        DescansoObjetivoSegundos = pres.DescansoObjetivoSegundos
                    };
                }

                if (ejec != null)
                {
                    var seriesEfectivas = ejec.SeriesRealizadas.Where(s => !s.EsCalentamiento).ToList();
                    var seriesCalentamiento = ejec.SeriesRealizadas.Where(s => s.EsCalentamiento).ToList();

                    dto.Ejecucion = new EjecucionResumenDto
                    {
                        CantidadSeriesTotales = ejec.SeriesRealizadas.Count,
                        CantidadSeriesEfectivas = seriesEfectivas.Count,
                        CantidadSeriesCalentamiento = seriesCalentamiento.Count,
                        VolumenTotalKg = ejec.SeriesRealizadas.Sum(Volumen),
                        VolumenEfectivoKg = seriesEfectivas.Sum(Volumen),
                        VolumenCalentamientoKg = seriesCalentamiento.Sum(Volumen),
                        DetallesSeries = ejec.SeriesRealizadas.OrderBy(s => s.OrdenSerie).Select(s => {
                            var serieDto = new SerieComparadaDto
                            {
                                OrdenSerie = s.OrdenSerie,
                                PesoLevantadoKg = s.PesoLevantadoKg,
                                Repeticiones = s.Repeticiones,
                                RIR = s.RIR,
                                EsCalentamiento = s.EsCalentamiento,
                                TiempoDescansoSegundos = s.TiempoDescansoSegundos,
                                Volumen = Volumen(s)
                            };

                            if (!s.EsCalentamiento && pres != null)
                            {
                                bool cumpleReps = s.Repeticiones >= pres.RepeticionesMinimas && s.Repeticiones <= pres.RepeticionesMaximas;
                                int desviacionReps = 0;
                                if (s.Repeticiones < pres.RepeticionesMinimas) desviacionReps = s.Repeticiones - pres.RepeticionesMinimas;
                                else if (s.Repeticiones > pres.RepeticionesMaximas) desviacionReps = s.Repeticiones - pres.RepeticionesMaximas;

                                serieDto.RepeticionesComparacion = new ComparacionMetricaDto
                                {
                                    CumpleObjetivo = cumpleReps,
                                    Desviacion = desviacionReps,
                                    Mensaje = cumpleReps ? "Dentro del rango" : $"{(desviacionReps < 0 ? "Faltaron" : "Excedieron")} {Math.Abs(desviacionReps)} {(enSegundos ? "s" : "reps")}."
                                };

                                if (pres.RIRObjetivo.HasValue && s.RIR.HasValue)
                                {
                                    bool cumpleRir = s.RIR.Value == pres.RIRObjetivo.Value;
                                    int desviacionRir = s.RIR.Value - pres.RIRObjetivo.Value;
                                    serieDto.RIRComparacion = new ComparacionMetricaDto
                                    {
                                        CumpleObjetivo = cumpleRir,
                                        Desviacion = desviacionRir,
                                        Mensaje = cumpleRir ? "RIR exacto" : $"Desviación de {desviacionRir} RIR"
                                    };
                                }
                            }
                            return serieDto;
                        }).ToList()
                    };
                }

                if (pres != null)
                {
                    int realizadasEfectivas = ejec?.SeriesRealizadas.Count(s => !s.EsCalentamiento) ?? 0;
                    dto.Comparacion.Series = new ComparacionSeriesDto
                    {
                        Prescritas = pres.SeriesObjetivo,
                        RealizadasEfectivas = realizadasEfectivas,
                        Diferencia = realizadasEfectivas - pres.SeriesObjetivo,
                        Mensaje = realizadasEfectivas == pres.SeriesObjetivo ? "Cumple objetivo" : $"{(realizadasEfectivas < pres.SeriesObjetivo ? "Faltaron" : "Excedieron")} {Math.Abs(realizadasEfectivas - pres.SeriesObjetivo)} series"
                    };
                }

                comparacionDto.Ejercicios.Add(dto);
            }

            comparacionDto.Ejercicios = comparacionDto.Ejercicios.OrderBy(e => e.Orden).ToList();

            return comparacionDto;
        }

        private SesionEntrenamientoResponseDto MapToResponseDto(SesionEntrenamiento sesion)
        {
            return new SesionEntrenamientoResponseDto
            {
                Id = sesion.Id,
                SocioId = sesion.SocioId,
                PrescripcionEntrenamientoId = sesion.PrescripcionEntrenamientoId,
                Inicio = sesion.Inicio,
                Fin = sesion.Fin,
                CaloriasQuemadas = sesion.CaloriasQuemadas,
                FrecuenciaCardiacaMaxima = sesion.FrecuenciaCardiacaMaxima,
                FrecuenciaCardiacaPromedio = sesion.FrecuenciaCardiacaPromedio,
                Ejercicios = sesion.EjerciciosEjecutados.OrderBy(e => e.Orden).Select(e => new SesionEjercicioResponseDto
                {
                    Id = e.Id,
                    EjercicioId = e.EjercicioId,
                    Orden = e.Orden,
                    Observaciones = e.Observaciones,
                    SeriesRealizadas = e.SeriesRealizadas.OrderBy(s => s.OrdenSerie).Select(s => new SerieRealizadaResponseDto
                    {
                        Id = s.Id,
                        OrdenSerie = s.OrdenSerie,
                        PesoLevantadoKg = s.PesoLevantadoKg,
                        Repeticiones = s.Repeticiones,
                        RIR = s.RIR,
                        EsCalentamiento = s.EsCalentamiento,
                        TiempoDescansoSegundos = s.TiempoDescansoSegundos
                    }).ToList()
                }).ToList()
            };
        }
    }
}