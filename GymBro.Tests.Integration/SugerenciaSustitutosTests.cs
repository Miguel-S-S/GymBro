using System.Net;
using System.Net.Http.Json;
using GymBro.Application.Services;
using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Domain.Exceptions;
using GymBro.Domain.Rules;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pgvector;
using Xunit;

namespace GymBro.Tests.Integration
{
    /// <summary>Generador falso de la 9b-2: devuelve el texto que la prueba le indica (o simula el modelo caido).</summary>
    public class GeneradorSustitutosFalso : IGeneradorSustitutos
    {
        public Func<PedidoSustitutos, string> Responder { get; set; } = _ => "{\"elegidos\": []}";
        public bool Caido { get; set; }
        public PedidoSustitutos? Ultimo { get; private set; }

        public Task<RespuestaGenerador> GenerarAsync(PedidoSustitutos pedido)
        {
            Ultimo = pedido;
            if (Caido) throw new ServicioExternoNoDisponibleException("El modelo de lenguaje local no está disponible en este momento. Intente más tarde.");
            return Task.FromResult(new RespuestaGenerador("modelo-falso", Responder(pedido)));
        }
    }

    public class ApiConSustitutosFalsos : GymBroApiFactory
    {
        public IaBusquedaFalsa Ia { get; } = new();
        public GeneradorSustitutosFalso Generador { get; } = new();

        protected override void ConfigurarExtra(IWebHostBuilder builder) =>
            builder.ConfigureTestServices(s =>
            {
                s.RemoveAll<IIaService>();
                s.AddSingleton<IIaService>(Ia);
                s.RemoveAll<IGeneradorSustitutos>();
                s.AddSingleton<IGeneradorSustitutos>(Generador);
            });
    }

    /// <summary>
    /// Etapa 9b-2 (caso a): sugerencia de sustitutos con un generador falso, contra PostgreSQL descartable. Escenario
    /// SINTETICO: G1 con revision humana, un socio con una rutina pendiente de revision (no es del protocolo) y otra del
    /// protocolo de piernas; candidatos con embedding; un ejercicio de otro gimnasio que nunca debe ser candidato.
    /// </summary>
    public class SugerenciaSustitutosTests : IClassFixture<SugerenciaSustitutosTests.Escenario>
    {
        private readonly Escenario _e;
        public SugerenciaSustitutosTests(Escenario e) { _e = e; }

        public sealed record Candidato(Guid Id, string Nombre);
        public sealed record Sugerido(Guid EjercicioId, string Nombre, string Justificacion);
        public sealed record Rechazado(Guid EjercicioId, List<string> Codigos);
        public sealed record Sugerencia(Guid LineaId, List<Candidato> Candidatos, List<Sugerido> Sugerencias, List<Rechazado> RechazadasPorLimites,
            bool RespetaConjunto, bool Descartada, string? MotivoDescarte, string? Modelo, double? LatenciaMs);

        private string Url(Guid rutina, Guid linea) => $"/api/revisiones/{rutina}/lineas/{linea}/sustitutos";

        private async Task<Sugerencia> SugerirAsync(Func<PedidoSustitutos, string> responder, Usuario? quien = null)
        {
            _e.Api.Generador.Responder = responder;
            var r = await _e.Api.ClienteComo(quien ?? _e.Dueno).PostAsJsonAsync(Url(_e.Rutina, _e.Linea), new { motivo = "molestia lumbar" });
            Assert.True(r.StatusCode == HttpStatusCode.OK, $"{(int)r.StatusCode}: {await r.Content.ReadAsStringAsync()}");
            return (await r.Content.ReadFromJsonAsync<Sugerencia>())!;
        }

        private static string Json(params (Guid Id, string J)[] elegidos) =>
            "{\"elegidos\": [" + string.Join(",", elegidos.Select(e => $"{{\"id\": \"{e.Id}\", \"justificacion\": \"{e.J}\"}}")) + "]}";

        private async Task RutinaSinCambiosAsync()
        {
            await _e.Api.ConDbAsync(async db =>
            {
                var p = await db.PrescripcionesEntrenamiento.IgnoreQueryFilters().Include(x => x.EjerciciosPrescritos).SingleAsync(x => x.Id == _e.Rutina);
                Assert.Equal(EstadoPrescripcion.PendienteRevision, p.Estado);
                Assert.Equal(_e.EjercicioActual, p.EjerciciosPrescritos.Single(l => l.Id == _e.Linea).EjercicioId);
            });
        }

        [Fact]
        public async Task Las_sugerencias_salen_solo_de_los_candidatos_recuperados_y_no_modifican_la_rutina()
        {
            // Se eligen dos candidatos que no violan limites (el isometrico se prueba aparte).
            var s = await SugerirAsync(p =>
            {
                var validos = p.Candidatos.Where(c => c.Id != _e.CandidatoEnSegundos).ToList();
                return Json((validos[0].Id, "misma zona sin carga axial"), (validos[1].Id, "alternativa en maquina"));
            });
            Assert.False(s.Descartada);
            Assert.True(s.RespetaConjunto);
            Assert.Equal("modelo-falso", s.Modelo);
            Assert.NotNull(s.LatenciaMs);
            Assert.Equal(2, s.Sugerencias.Count);
            Assert.All(s.Sugerencias, x => Assert.Contains(s.Candidatos, c => c.Id == x.EjercicioId));
            // Los candidatos respetan los filtros duros y no incluyen el ejercicio actual ni otros de la rutina.
            Assert.DoesNotContain(s.Candidatos, c => c.Id == _e.DeOtroGimnasio || c.Id == _e.EjercicioActual || c.Id == _e.OtroDeLaRutina);
            await RutinaSinCambiosAsync();

            var auditado = await _e.Leer(db => db.AuditLogs.AsNoTracking().Where(a => a.Accion == "SUGERENCIA_SUSTITUTOS" && a.EntidadId == _e.Linea.ToString())
                .OrderByDescending(a => a.Fecha).Select(a => a.Detalle).FirstAsync());
            Assert.Contains("respeta_conjunto=si", auditado);
            Assert.Contains("sugeridos=2", auditado);
            Assert.DoesNotContain("lumbar", auditado);   // sin texto libre ni datos de salud en la auditoria
        }

        [Fact]
        public async Task Un_id_inventado_descarta_toda_la_salida()
        {
            var s = await SugerirAsync(p => Json((p.Candidatos[0].Id, "valido"), (Guid.NewGuid(), "inventado")));
            Assert.True(s.Descartada);
            Assert.Equal("candidato_fuera_del_conjunto", s.MotivoDescarte);
            Assert.False(s.RespetaConjunto);
            Assert.Empty(s.Sugerencias);
            await RutinaSinCambiosAsync();
        }

        [Fact]
        public async Task Un_ejercicio_que_existe_pero_no_fue_recuperado_tambien_descarta_la_salida()
        {
            var s = await SugerirAsync(_ => Json((_e.DeOtroGimnasio, "de otro gimnasio")));
            Assert.True(s.Descartada);
            Assert.Equal("candidato_fuera_del_conjunto", s.MotivoDescarte);
        }

        [Theory]
        [InlineData("esto no es json")]
        [InlineData("{\"elegidos\": \"prensa\"}")]
        [InlineData("{\"otros\": []}")]
        [InlineData("{\"elegidos\": [{\"id\": \"prensa-de-piernas\"}]}")]
        [InlineData("")]
        public async Task Una_salida_mal_formada_se_descarta(string texto)
        {
            var s = await SugerirAsync(_ => texto);
            Assert.True(s.Descartada);
            Assert.Equal("salida_mal_formada", s.MotivoDescarte);
            Assert.Empty(s.Sugerencias);
        }

        [Fact]
        public async Task Un_candidato_que_viola_los_limites_se_rechaza_por_el_validador()
        {
            // La linea es de 3 a 5 repeticiones: con el candidato "en segundos" quedaria de 3 a 5 s, debajo del minimo de 5 s (LIM-SEGUNDOS).
            var s = await SugerirAsync(p => Json((_e.CandidatoEnSegundos, "isometrico"), (p.Candidatos.First(c => c.Id != _e.CandidatoEnSegundos).Id, "ok")));
            Assert.False(s.Descartada);
            var rechazado = Assert.Single(s.RechazadasPorLimites);
            Assert.Equal(_e.CandidatoEnSegundos, rechazado.EjercicioId);
            Assert.NotEmpty(rechazado.Codigos);
            Assert.DoesNotContain(s.Sugerencias, x => x.EjercicioId == _e.CandidatoEnSegundos);
            Assert.Single(s.Sugerencias);
        }

        [Fact]
        public async Task Sobre_una_linea_del_protocolo_de_piernas_responde_400()
        {
            var r = await _e.Api.ClienteComo(_e.Dueno).PostAsJsonAsync(Url(_e.RutinaProtocolo, _e.LineaProtocolo), new { motivo = "molestia lumbar" });
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
            Assert.Contains("protocolo de piernas", await r.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task Con_el_modelo_caido_responde_503_sin_efectos()
        {
            _e.Api.Generador.Caido = true;
            try
            {
                var r = await _e.Api.ClienteComo(_e.Dueno).PostAsJsonAsync(Url(_e.Rutina, _e.Linea), new { motivo = "molestia lumbar" });
                Assert.Equal(HttpStatusCode.ServiceUnavailable, r.StatusCode);
                Assert.Contains("no está disponible", await r.Content.ReadAsStringAsync());
            }
            finally
            {
                _e.Api.Generador.Caido = false;
            }
            await RutinaSinCambiosAsync();
        }

        [Fact]
        public async Task Autorizacion_socio_y_plataforma_403_trainer_no_asignado_404_sin_motivo_400()
        {
            HttpStatusCode Codigo(HttpResponseMessage r) => r.StatusCode;
            var cuerpo = new { motivo = "molestia lumbar" };
            Assert.Equal(HttpStatusCode.Forbidden, Codigo(await _e.Api.ClienteComo(_e.CuentaSocio).PostAsJsonAsync(Url(_e.Rutina, _e.Linea), cuerpo)));
            Assert.Equal(HttpStatusCode.Forbidden, Codigo(await _e.Api.ClienteComo(_e.Plataforma).PostAsJsonAsync(Url(_e.Rutina, _e.Linea), cuerpo)));
            Assert.Equal(HttpStatusCode.NotFound, Codigo(await _e.Api.ClienteComo(_e.TrainerNoAsignado).PostAsJsonAsync(Url(_e.Rutina, _e.Linea), cuerpo)));
            Assert.Equal(HttpStatusCode.OK, Codigo(await _e.Api.ClienteComo(_e.TrainerAsignado).PostAsJsonAsync(Url(_e.Rutina, _e.Linea), cuerpo)));
            Assert.Equal(HttpStatusCode.BadRequest, Codigo(await _e.Api.ClienteComo(_e.Dueno).PostAsJsonAsync(Url(_e.Rutina, _e.Linea), new { motivo = "" })));
            Assert.Equal(HttpStatusCode.NotFound, Codigo(await _e.Api.ClienteComo(_e.Dueno).PostAsJsonAsync(Url(Guid.NewGuid(), _e.Linea), cuerpo)));
        }

        public class Escenario : IAsyncLifetime
        {
            public ApiConSustitutosFalsos Api { get; } = new();
            public Gimnasio G1 { get; } = new() { Nombre = "Sustitutos 1", Codigo = "SUST-1", RequiereAprobacionRutinas = true };
            public Gimnasio G2 { get; } = new() { Nombre = "Sustitutos 2", Codigo = "SUST-2" };
            public Guid Rutina { get; } = Guid.NewGuid();
            public Guid Linea { get; } = Guid.NewGuid();
            public Guid RutinaProtocolo { get; } = Guid.NewGuid();
            public Guid LineaProtocolo { get; } = Guid.NewGuid();
            public Guid EjercicioActual { get; } = Guid.NewGuid();
            public Guid OtroDeLaRutina { get; } = Guid.NewGuid();
            public Guid DeOtroGimnasio { get; } = Guid.NewGuid();
            public Guid CandidatoEnSegundos { get; } = Guid.NewGuid();
            public Usuario Dueno { get; private set; } = null!;
            public Usuario TrainerAsignado { get; private set; } = null!;
            public Usuario TrainerNoAsignado { get; private set; } = null!;
            public Usuario CuentaSocio { get; private set; } = null!;
            public Usuario Plataforma { get; private set; } = null!;

            public async Task<T> Leer<T>(Func<GymBro.Infrastructure.Persistence.GymBroDbContext, Task<T>> consulta)
            {
                T v = default!;
                await Api.ConDbAsync(async db => v = await consulta(db));
                return v;
            }

            public async Task InitializeAsync()
            {
                await Api.InitializeAsync();
                var socio = new Socio
                {
                    Id = Guid.NewGuid(), Nombre = "Lucia", Apellido = "Sintetica", Email = "lucia@sustitutos.test", Genero = "F",
                    FechaNacimiento = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc), GimnasioId = G1.Id,
                    Objetivo = ObjetivoEntrenamiento.Mantenimiento, Nivel = NivelExperiencia.Intermedio, OrigenDatos = OrigenDatos.Sintetico,
                    ConsentimientoDatosSaludFecha = DateTime.UtcNow,
                };
                Dueno = new Usuario { Email = "dueno@sustitutos.test", NombreCompleto = "Dueno", Rol = RolUsuario.AdministradorGimnasio, GimnasioId = G1.Id, PasswordHash = "-" };
                TrainerAsignado = new Usuario { Email = "t1@sustitutos.test", NombreCompleto = "T1", Rol = RolUsuario.Entrenador, GimnasioId = G1.Id, PasswordHash = "-" };
                TrainerNoAsignado = new Usuario { Email = "t2@sustitutos.test", NombreCompleto = "T2", Rol = RolUsuario.Entrenador, GimnasioId = G1.Id, PasswordHash = "-" };
                CuentaSocio = new Usuario { Email = socio.Email, NombreCompleto = "Lucia", Rol = RolUsuario.Socio, SocioId = socio.Id, PasswordHash = "-" };
                Plataforma = new Usuario { Email = "plataforma@sustitutos.test", NombreCompleto = "Plataforma", Rol = RolUsuario.SuperAdministrador, PasswordHash = "-" };

                var mancuerna = CatalogoEquipamiento.IdDe(CatalogoEquipamiento.Mancuerna);
                Ejercicio Ej(Guid id, string nombre, float d, Guid? gimnasio = null, TipoMedida medida = TipoMedida.Repeticiones) => new()
                {
                    Id = id, Nombre = nombre, ZonaObjetivo = "Legs", MusculoPrincipal = "Cuadriceps", TipoEquipamientoId = mancuerna,
                    GimnasioId = gimnasio, Origen = gimnasio is null ? OrigenEjercicio.Proyecto : OrigenEjercicio.Gimnasio, TipoMedida = medida,
                    Embedding = new Vector(IaBusquedaFalsa.Base(d)),
                };

                await Api.ConDbAsync(db =>
                {
                    db.Gimnasios.AddRange(G1, G2);
                    db.Socios.Add(socio);
                    db.Usuarios.AddRange(Dueno, TrainerAsignado, TrainerNoAsignado, CuentaSocio, Plataforma);
                    db.Asignaciones.Add(new AsignacionEntrenadorSocio { GimnasioId = G1.Id, EntrenadorUsuarioId = TrainerAsignado.Id, SocioId = socio.Id });
                    db.Ejercicios.AddRange(
                        Ej(EjercicioActual, "Sentadilla sintetica", 0f),
                        Ej(OtroDeLaRutina, "Zancada sintetica", 0.01f),
                        Ej(DeOtroGimnasio, "Intruso de G2", 0f, gimnasio: G2.Id),
                        Ej(CandidatoEnSegundos, "Sentadilla isometrica sintetica", 0.02f, medida: TipoMedida.Segundos),
                        Ej(Guid.NewGuid(), "Prensa sintetica", 0.05f),
                        Ej(Guid.NewGuid(), "Sentadilla bulgara sintetica", 0.1f),
                        Ej(Guid.NewGuid(), "Extension sintetica", 0.2f));
                    db.PrescripcionesEntrenamiento.Add(new PrescripcionEntrenamiento
                    {
                        Id = Rutina, SocioId = socio.Id, OrigenGeneracion = "PRUEBA", Estado = EstadoPrescripcion.PendienteRevision, OrigenDatos = OrigenDatos.Sintetico,
                        EjerciciosPrescritos =
                        {
                            new PrescripcionEjercicio { Id = Linea, PrescripcionEntrenamientoId = Rutina, EjercicioId = EjercicioActual, Orden = 1, SeriesObjetivo = 3, RepeticionesMinimas = 3, RepeticionesMaximas = 5, RIRObjetivo = 2, DescansoObjetivoSegundos = 90 },
                            new PrescripcionEjercicio { PrescripcionEntrenamientoId = Rutina, EjercicioId = OtroDeLaRutina, Orden = 2, SeriesObjetivo = 3, RepeticionesMinimas = 8, RepeticionesMaximas = 12, RIRObjetivo = 2, DescansoObjetivoSegundos = 90 },
                        },
                    });
                    db.PrescripcionesEntrenamiento.Add(new PrescripcionEntrenamiento
                    {
                        Id = RutinaProtocolo, SocioId = socio.Id, OrigenGeneracion = "PROTOCOLO_PIERNAS_DETERMINISTA", Estado = EstadoPrescripcion.PendienteRevision,
                        ProtocoloCodigo = "PIERNAS", ProtocoloVersion = "piernas-1.0", NumeroGeneracion = 1, OrigenDatos = OrigenDatos.Sintetico,
                        EjerciciosPrescritos =
                        {
                            new PrescripcionEjercicio { Id = LineaProtocolo, PrescripcionEntrenamientoId = RutinaProtocolo, EjercicioId = EjercicioActual, Orden = 1, SeriesObjetivo = 3, RepeticionesMinimas = 8, RepeticionesMaximas = 12, RIRObjetivo = 2, DescansoObjetivoSegundos = 150, Rol = RolEjercicioProtocolo.Referencia, Grupo = GrupoMuscularPiernas.Cuadriceps },
                        },
                    });
                    return Task.CompletedTask;
                });
            }

            public async Task DisposeAsync() => await Api.DisposeAsync();
        }
    }
}
