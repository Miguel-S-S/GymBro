using GymBro.Application.DTOs;
using GymBro.Application.DTOs.Acceso;
using GymBro.Application.DTOs.AsistenciaCuotas;
using GymBro.Application.DTOs.Bienestar;
using GymBro.Application.Services;
using GymBro.Application.Sintetico;
using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Domain.Rules;
using Microsoft.Extensions.DependencyInjection;

namespace GymBro.Infrastructure.Sintetico
{
    /// <summary>Resultado de una generacion.</summary>
    public sealed record ResultadoGeneracion(Guid GimnasioId, string GimnasioCodigo, ResumenSintetico Resumen, int Abandonos,
        int GeneracionesFallidas, IReadOnlyList<Guid> SociosIds);

    /// <summary>
    /// Etapa 7e: generador sintetico reproducible. Crea un gimnasio y N socios con origen Sintetico (ids derivados de la
    /// semilla) y simula dia por dia las semanas pedidas invocando los servicios reales: limitaciones articulares,
    /// medicion, planificador de piernas (una generacion por semana), registro de sesion y series (que dispara las reglas de
    /// seguridad), bienestar diario (que dispara R-FC-1), asistencia y cuotas. Los parametros estan en ParametrosSinteticos y
    /// en docs/pfc/sintetico-parametros.md. Requiere el registro de ContextoSintetico, UsuarioActualSintetico y
    /// RelojSintetico (modo generador de Program.cs o pruebas).
    /// </summary>
    public sealed class GeneradorSintetico
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly ContextoSintetico _ctx;
        private readonly RelojSintetico _reloj;
        private static readonly TimeZoneInfo Zona = TimeZoneInfo.FindSystemTimeZoneById(DiaLocal.ZonaHoraria);

        public GeneradorSintetico(IServiceScopeFactory scopes, ContextoSintetico ctx, RelojSintetico reloj)
        {
            _scopes = scopes;
            _ctx = ctx;
            _reloj = reloj;
        }

        private sealed class SocioSimulado
        {
            public required Socio Socio { get; init; }
            public required Usuario Cuenta { get; init; }
            public required AzarDeterminista Azar { get; init; }
            public double FactorCarga { get; init; }
            public double Adherencia { get; init; }
            public HashSet<DayOfWeek> Dias { get; init; } = new();
            public int Franja { get; init; }
            public double FcBase { get; init; }
            public decimal PesoBase { get; init; }
            public RutinaPendienteDto? Rutina { get; set; }
            public bool Abandono { get; set; }
            public List<(DateOnly Dia, bool Asistio)> Programadas { get; } = new();
            public Dictionary<(int Anio, int Mes), DateOnly?> PagoPrevisto { get; } = new();
        }

        public async Task<ResultadoGeneracion> GenerarAsync(ParametrosSinteticos p, Action<string>? log = null)
        {
            p.Validar();
            var gimnasioId = AzarDeterminista.IdDerivado(p.Semilla, "gimnasio");
            var codigo = IAlmacenSintetico.PrefijoCodigo + ((uint)p.Semilla).ToString("X8");
            var dueno = new Usuario
            {
                Id = AzarDeterminista.IdDerivado(p.Semilla, "dueno"), Email = $"dueno.{p.Semilla}@sintetico.gymbro.invalid",
                NombreCompleto = $"Dueno sintetico {p.Semilla}", Rol = RolUsuario.AdministradorGimnasio, GimnasioId = gimnasioId,
                PasswordHash = "-", FechaAlta = Utc(p.Inicio.AddDays(-30), 9, 0),
            };

            _ctx.ComoSistema();
            await EnAmbitoAsync<IAlmacenSintetico>(async a =>
            {
                if (await a.ExisteGimnasioAsync(gimnasioId))
                    throw new InvalidOperationException($"Ya existe el gimnasio sintetico de la semilla {p.Semilla} ({codigo}). Ejecute --eliminar-sintetico antes.");
                await a.CrearGimnasioAsync(new Gimnasio
                {
                    Id = gimnasioId, Nombre = $"Gimnasio sintetico (semilla {p.Semilla})", Codigo = codigo,
                    RequiereAprobacionRutinas = false, FechaAlta = Utc(p.Inicio.AddDays(-30), 9, 0),
                }, dueno);
            });

            var clases = await EnAmbitoAsync<AlmacenSintetico, Dictionary<Guid, (ClaseEjercicio Clase, GrupoMuscularPiernas Grupo)>>(
                a => a.ClasesProtocoloAsync(new ProtocoloPiernasConfig().Version));
            var socios = new List<SocioSimulado>();
            for (var i = 0; i < p.Socios; i++) socios.Add(await CrearSocioAsync(p, i, gimnasioId, dueno));
            log?.Invoke($"Creados {socios.Count} socios sinteticos en {codigo}.");

            var abandonos = 0;
            var fallidas = 0;
            var fin = p.Inicio.AddDays(7 * p.Semanas);
            for (var dia = p.Inicio; dia < fin; dia = dia.AddDays(1))
            {
                // Cuotas: el primer dia de la simulacion y cada dia 1 del mes.
                if (dia == p.Inicio || dia.Day == 1) await GenerarCuotasAsync(p, dia, socios, dueno, gimnasioId);

                // Una generacion de rutina por semana (lunes), para los socios que no abandonaron.
                if (dia.DayOfWeek == DayOfWeek.Monday)
                {
                    foreach (var s in socios.Where(s => !s.Abandono))
                    {
                        Fijar(dia, 6, 0);
                        _ctx.ComoPersonal(RolUsuario.AdministradorGimnasio, dueno.Id, gimnasioId);
                        try
                        {
                            s.Rutina = await EnAmbitoAsync<IPrescripcionPiernasService, RutinaPendienteDto>(x => x.GenerarAsync(s.Socio.Id));
                        }
                        catch (Exception ex) when (ex is Domain.Exceptions.ReglaNegocioException or CatalogosIncompletosException)
                        {
                            fallidas++;
                            log?.Invoke($"Generacion de rutina rechazada para el socio {s.Socio.Id}: {ex.Message}");
                        }
                    }
                }

                foreach (var s in socios)
                {
                    if (s.Abandono) continue;
                    // Variables latentes del dia: siempre se sortean (misma cantidad de sorteos por dia).
                    var fatiga = ModeloSintetico.Fatiga(s.Azar.Normal(0, 1), p);
                    var ingesta = s.Azar.Categorica(p.ProporcionIngesta);
                    var cargaBienestar = s.Azar.Bernoulli(p.ProbabilidadBienestarDiario);

                    if (s.Dias.Contains(dia.DayOfWeek))
                    {
                        var asiste = s.Azar.Bernoulli(s.Adherencia) && s.Rutina is not null;
                        s.Programadas.Add((dia, asiste));
                        if (asiste) await SesionAsync(p, s, dia, fatiga, ingesta, clases, dueno, gimnasioId);
                    }

                    if (cargaBienestar) await BienestarAsync(p, s, dia, fatiga, ingesta);

                    if (s.PagoPrevisto.Where(kv => kv.Value == dia).Select(kv => kv.Key).ToList() is { Count: > 0 } pagos)
                        foreach (var periodo in pagos) await PagarAsync(s, periodo, dia, dueno, gimnasioId);
                }

                // Domingo: riesgo de abandono segun la adherencia de las ultimas 4 semanas y la morosidad.
                if (dia.DayOfWeek == DayOfWeek.Sunday)
                {
                    foreach (var s in socios.Where(s => !s.Abandono))
                    {
                        var recientes = s.Programadas.Where(x => x.Dia > dia.AddDays(-28)).ToList();
                        var adherencia = recientes.Count == 0 ? 1.0 : recientes.Count(x => x.Asistio) / (double)recientes.Count;
                        var moroso = s.PagoPrevisto.Any(kv => VencimientoDe(kv.Key, p) < dia && (kv.Value is null || kv.Value > dia));
                        if (s.Azar.Bernoulli(ModeloSintetico.RiesgoAbandono(adherencia, moroso, p)))
                        {
                            s.Abandono = true;
                            abandonos++;
                        }
                    }
                }
            }

            _ctx.ComoSistema();
            var resumen = await EnAmbitoAsync<IAlmacenSintetico, ResumenSintetico>(a => a.ResumirAsync(gimnasioId));
            log?.Invoke($"Listo: {resumen.Sesiones} sesiones, {resumen.Series} series, {resumen.Bienestar} registros de bienestar, " +
                        $"{resumen.Asistencias} asistencias, {resumen.Cuotas} cuotas, {abandonos} abandonos. SHA-256 {resumen.Sha256}");
            return new ResultadoGeneracion(gimnasioId, codigo, resumen, abandonos, fallidas, socios.Select(s => s.Socio.Id).ToList());
        }

        public async Task<EliminacionSintetica> EliminarAsync()
        {
            _ctx.ComoSistema();
            return await EnAmbitoAsync<IAlmacenSintetico, EliminacionSintetica>(a => a.EliminarAsync());
        }

        // ---------------- Pasos ----------------

        private async Task<SocioSimulado> CrearSocioAsync(ParametrosSinteticos p, int i, Guid gimnasioId, Usuario dueno)
        {
            var azar = new AzarDeterminista(p.Semilla, $"socio:{i}");
            var nivel = (NivelExperiencia)(azar.Categorica(p.ProporcionNivel) + 1);
            var genero = azar.Bernoulli(0.5) ? "F" : "M";
            var edad = azar.Entero(p.EdadMinima, p.EdadMaxima + 1);
            var antiguedadDias = azar.Entero(0, 721);
            var socio = new Socio
            {
                Id = AzarDeterminista.IdDerivado(p.Semilla, $"socio:{i}"),
                Nombre = $"Sintetico{i + 1:D3}", Apellido = $"Semilla{p.Semilla}",
                Email = $"s{i + 1:D3}.{p.Semilla}@sintetico.gymbro.invalid", Genero = genero,
                FechaNacimiento = new DateTime(p.Inicio.Year - edad, 1 + azar.Entero(0, 12), 1 + azar.Entero(0, 28), 0, 0, 0, DateTimeKind.Utc),
                GimnasioId = gimnasioId, Estado = GymBro.Domain.Enums.EstadoSocio.Activo,
                Objetivo = (ObjetivoEntrenamiento)(azar.Entero(0, 3) + 1), Nivel = nivel,
                OrigenDatos = OrigenDatos.Sintetico,
                ConsentimientoDatosSaludFecha = Utc(p.Inicio.AddDays(-antiguedadDias), 10, 0),
                FechaAlta = Utc(p.Inicio.AddDays(-antiguedadDias), 10, 0),
            };
            var cuenta = new Usuario
            {
                Id = AzarDeterminista.IdDerivado(p.Semilla, $"cuenta:{i}"), Email = socio.Email, NombreCompleto = socio.NombreCompleto,
                Rol = RolUsuario.Socio, SocioId = socio.Id, PasswordHash = "-", FechaAlta = socio.FechaAlta,
            };
            _ctx.ComoSistema();
            await EnAmbitoAsync<IAlmacenSintetico>(a => a.CrearSocioAsync(socio, cuenta));

            var mujer = genero == "F";
            var peso = Math.Round((decimal)azar.Normal(mujer ? 62 : 78, 9), 1);
            var altura = Math.Round((decimal)azar.Normal(mujer ? 163 : 176, 7), 1);
            var rodilla = azar.Bernoulli(p.ProbabilidadRodillaModerada) ? NivelLimitacion.Moderada : NivelLimitacion.SinLimitacion;
            var lumbar = azar.Bernoulli(p.ProbabilidadLumbarModerada) ? NivelLimitacion.Moderada : NivelLimitacion.SinLimitacion;

            Fijar(p.Inicio.AddDays(-1), 10, 0);
            _ctx.ComoPersonal(RolUsuario.AdministradorGimnasio, dueno.Id, gimnasioId);
            await EnAmbitoAsync<IMedicionService>(m => m.RegistrarMedicionAsync(new MedicionCreateDto { SocioId = socio.Id, PesoKg = peso, AlturaCm = altura }));
            await EnAmbitoAsync<ILimitacionesArticularesService>(l => l.ActualizarAsync(socio.Id,
                new LimitacionesArticularesDto { Rodilla = rodilla.ToString(), Lumbar = lumbar.ToString() }));

            var dias = new HashSet<DayOfWeek>();
            var candidatos = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday };
            while (dias.Count < p.SesionesPorSemana) dias.Add(candidatos[azar.Entero(0, candidatos.Count)]);

            return new SocioSimulado
            {
                Socio = socio, Cuenta = cuenta, Azar = azar,
                FactorCarga = p.CargaInicialMultiarticularKg[p.IndiceNivel(nivel)] * azar.Uniforme(p.FactorIndividualMin, p.FactorIndividualMax),
                Adherencia = azar.Uniforme(p.AdherenciaMin, p.AdherenciaMax),
                Dias = dias, Franja = azar.Categorica(p.ProporcionFranja),
                FcBase = azar.Normal(p.FcReposoMedia, p.FcReposoDesvioEntreSocios), PesoBase = peso,
            };
        }

        private async Task SesionAsync(ParametrosSinteticos p, SocioSimulado s, DateOnly dia, int fatiga, int ingesta,
            Dictionary<Guid, (ClaseEjercicio Clase, GrupoMuscularPiernas Grupo)> clases, Usuario dueno, Guid gimnasioId)
        {
            var rutina = s.Rutina!;
            var (desde, ancho) = s.Franja switch { 0 => (7, 2), 1 => (12, 2), _ => (18, 3) };
            var minutos = 5 * s.Azar.Entero(0, ancho * 12);
            var inicio = Utc(dia, desde, 0).AddMinutes(minutos);
            var semanas = (dia.DayNumber - p.Inicio.DayNumber) / 7.0;
            var ganancia = p.GananciaSemanalPorNivel[p.IndiceNivel(s.Socio.Nivel)];

            var lineas = rutina.Ejercicios.OrderBy(l => l.Orden).ToList();
            var completa = s.Azar.Bernoulli(p.ProbabilidadSeriesCompletas);
            var lineaIncompleta = completa ? -1 : s.Azar.Entero(0, lineas.Count);

            var ejercicios = new List<SesionEjercicioRequestDto>();
            for (var k = 0; k < lineas.Count; k++)
            {
                var l = lineas[k];
                var (clase, grupo) = clases.TryGetValue(l.EjercicioId, out var c) ? c : (ClaseEjercicio.Multiarticular, GrupoMuscularPiernas.Cuadriceps);
                var factor = grupo == GrupoMuscularPiernas.Pantorrillas ? p.FactorPantorrillas
                    : clase == ClaseEjercicio.Aislamiento ? p.FactorAislamiento : 1.0;
                var verdadera = ModeloSintetico.CargaEsperada(s.FactorCarga * factor, ganancia, semanas);
                var series = new List<SerieRealizadaRequestDto>();
                var orden = 1;
                if (k == 0)
                    series.Add(new SerieRealizadaRequestDto { OrdenSerie = orden++, PesoLevantadoKg = Redondear(verdadera * 0.5, p), Repeticiones = 10, EsCalentamiento = true });
                var cantidad = l.SeriesObjetivo - (k == lineaIncompleta ? 1 : 0);
                for (var n = 0; n < cantidad; n++)
                {
                    var carga = Redondear(verdadera * (1 + p.RuidoCarga * s.Azar.Normal(0, 1)), p);
                    var reps = ModeloSintetico.Repeticiones(s.Azar.Normal(0, 1), l.RepeticionesMinimas, l.RepeticionesMaximas, p);
                    var rirObjetivo = l.RIRObjetivo ?? 2;
                    var rir = ModeloSintetico.Rir(ModeloSintetico.MediaRir(rirObjetivo, fatiga, ingesta, p), s.Azar.Normal(0, 1), p);
                    series.Add(new SerieRealizadaRequestDto
                    {
                        OrdenSerie = orden++, PesoLevantadoKg = carga, Repeticiones = reps, RIR = rir,
                        TiempoDescansoSegundos = l.DescansoObjetivoSegundos,
                    });
                }
                if (series.Count == 0) continue;
                ejercicios.Add(new SesionEjercicioRequestDto { EjercicioId = l.EjercicioId, Orden = l.Orden, PrescripcionEjercicioId = l.Id, SeriesRealizadas = series });
            }

            _ctx.ComoPersonal(RolUsuario.AdministradorGimnasio, dueno.Id, gimnasioId);
            _reloj.Fijar(inicio.AddMinutes(-5));
            await EnAmbitoAsync<IAsistenciaService, RegistroAsistenciaDto>(a => a.RegistrarManualAsync(s.Socio.Id));
            _reloj.Fijar(inicio.AddMinutes(60));
            await EnAmbitoAsync<ISesionEntrenamientoService, SesionEntrenamientoResponseDto>(x => x.RegistrarSesionAsync(new RegistrarSesionEntrenamientoRequest
            {
                SocioId = s.Socio.Id, PrescripcionEntrenamientoId = rutina.Id, Inicio = inicio, Fin = inicio.AddMinutes(60),
                Ejercicios = ejercicios,
            }));
        }

        private async Task BienestarAsync(ParametrosSinteticos p, SocioSimulado s, DateOnly dia, int fatiga, int ingesta)
        {
            var a = s.Azar;
            var sueno = Math.Clamp(Math.Round(a.Normal(7, 1) * 2, MidpointRounding.AwayFromZero) / 2, 3, 11);
            var estres = ModeloSintetico.Recortar((int)Math.Round(a.Normal(3.5, 1.2), MidpointRounding.AwayFromZero), 1, 7);
            var fc = ModeloSintetico.Recortar((int)Math.Round(s.FcBase + a.Normal(0, p.FcReposoDesvioDiario), MidpointRounding.AwayFromZero), 40, 100);
            var agua = Math.Clamp(Math.Round(a.Normal(2, 0.5), 1), 0, 10);
            var peso = Math.Round(s.PesoBase + (decimal)a.Normal(0, 0.3), 1);
            Fijar(dia, 21, 0);
            _ctx.ComoSocio(s.Cuenta.Id, s.Socio.Id);
            await EnAmbitoAsync<IBienestarService, RegistroBienestarDto>(b => b.GuardarAsync(dia, new RegistroBienestarRequest
            {
                HorasSueno = (decimal)sueno, Estres = (short)estres, Fatiga = (short)fatiga, PesoKg = peso,
                NutricionCategoria = ((NutricionCategoria)(ingesta + 1)).ToString(), IngestaAguaLitros = (decimal)agua,
                FrecuenciaCardiacaReposo = (short)fc,
            }));
        }

        private async Task GenerarCuotasAsync(ParametrosSinteticos p, DateOnly dia, List<SocioSimulado> socios, Usuario dueno, Guid gimnasioId)
        {
            var periodo = (dia.Year, dia.Month);
            var vencimiento = VencimientoDe(periodo, p);
            Fijar(dia, 8, 0);
            _ctx.ComoPersonal(RolUsuario.AdministradorGimnasio, dueno.Id, gimnasioId);
            await EnAmbitoAsync<ICuotaService, GenerarCuotasResultadoDto>(c => c.GenerarMesAsync(new GenerarCuotasRequest
            {
                Anio = dia.Year, Mes = dia.Month, Monto = p.MontoCuota, Vencimiento = vencimiento,
            }));
            // Todos los socios siguen Activos (el abandono no cambia el estado administrativo): todos tienen cuota.
            foreach (var s in socios)
            {
                DateOnly? pago = null;
                if (s.Azar.Bernoulli(p.ProbabilidadPagoPuntual))
                    pago = dia.AddDays(s.Azar.Entero(0, Math.Max(1, vencimiento.DayNumber - dia.DayNumber + 1)));
                else if (s.Azar.Bernoulli(p.ProbabilidadPagoTardio))
                    pago = vencimiento.AddDays(s.Azar.Entero(1, 21));
                s.PagoPrevisto[periodo] = pago;
            }
        }

        private async Task PagarAsync(SocioSimulado s, (int Anio, int Mes) periodo, DateOnly dia, Usuario dueno, Guid gimnasioId)
        {
            Fijar(dia, 11, 0);
            _ctx.ComoPersonal(RolUsuario.AdministradorGimnasio, dueno.Id, gimnasioId);
            var cuotas = await EnAmbitoAsync<ICuotaService, IReadOnlyList<CuotaDto>>(c => c.ListarPeriodoAsync(periodo.Anio, periodo.Mes, null));
            var cuota = cuotas.FirstOrDefault(c => c.SocioId == s.Socio.Id && c.Estado == nameof(EstadoCuota.Pendiente));
            if (cuota is null) return;
            await EnAmbitoAsync<ICuotaService, CuotaDto>(c => c.RegistrarPagoAsync(cuota.Id, new RegistrarPagoRequest { FechaPago = dia }));
        }

        // ---------------- Utilidades ----------------

        private static DateOnly VencimientoDe((int Anio, int Mes) periodo, ParametrosSinteticos p) =>
            new(periodo.Anio, periodo.Mes, Math.Min(p.DiaVencimiento, DateTime.DaysInMonth(periodo.Anio, periodo.Mes)));

        private static decimal Redondear(double kg, ParametrosSinteticos p) =>
            (decimal)Math.Max(p.RedondeoCargaKg, Math.Round(kg / p.RedondeoCargaKg, MidpointRounding.AwayFromZero) * p.RedondeoCargaKg);

        /// <summary>Instante UTC de una hora local del dia indicado.</summary>
        private static DateTime Utc(DateOnly dia, int hora, int minuto) =>
            TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(dia.ToDateTime(new TimeOnly(hora, minuto)), DateTimeKind.Unspecified), Zona);

        private void Fijar(DateOnly dia, int hora, int minuto) => _reloj.Fijar(new DateTimeOffset(Utc(dia, hora, minuto), TimeSpan.Zero));

        private async Task EnAmbitoAsync<TServicio>(Func<TServicio, Task> accion) where TServicio : notnull
        {
            using var scope = _scopes.CreateScope();
            await accion(scope.ServiceProvider.GetRequiredService<TServicio>());
        }

        private async Task<TResultado> EnAmbitoAsync<TServicio, TResultado>(Func<TServicio, Task<TResultado>> accion) where TServicio : notnull
        {
            using var scope = _scopes.CreateScope();
            return await accion(scope.ServiceProvider.GetRequiredService<TServicio>());
        }
    }
}
