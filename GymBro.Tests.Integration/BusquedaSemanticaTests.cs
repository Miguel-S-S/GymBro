using System.Net;
using System.Net.Http.Json;
using GymBro.Application.Services;
using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Domain.Rules;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pgvector;
using Xunit;

namespace GymBro.Tests.Integration
{
    /// <summary>IA simulada para la busqueda: toda consulta se convierte en el vector e0; "caida" simula IA no disponible.</summary>
    public class IaBusquedaFalsa : IIaService
    {
        public bool Caida { get; set; }
        public int Llamadas;

        public static float[] Base(float segundo = 0f)
        {
            var v = new float[768];
            v[0] = 1f;
            v[1] = segundo;
            return v;
        }

        public Task<float[]> GenerarEmbeddingAsync(string texto)
        {
            Interlocked.Increment(ref Llamadas);
            if (Caida) throw new HttpRequestException("Ollama no disponible (simulado).");
            return Task.FromResult(Base());
        }
    }

    public class ApiConIaBusqueda : GymBroApiFactory
    {
        public IaBusquedaFalsa Ia { get; } = new();

        protected override void ConfigurarExtra(IWebHostBuilder builder) =>
            builder.ConfigureTestServices(s =>
            {
                s.RemoveAll<IIaService>();
                s.AddSingleton<IIaService>(Ia);
            });
    }

    /// <summary>
    /// Etapa 9b-1: filtros duros de la busqueda semantica contra PostgreSQL + pgvector descartable. Escenario SINTETICO:
    /// tres "intrusos" con distancia 0 a la consulta (inactivo, de otro gimnasio y con barra, que G1 no tiene) y tres
    /// validos un poco mas lejos. G1 declaro solo mancuernas; el peso corporal es "siempre disponible".
    /// </summary>
    public class BusquedaSemanticaTests : IClassFixture<BusquedaSemanticaTests.Escenario>
    {
        private readonly Escenario _e;
        public BusquedaSemanticaTests(Escenario e) { _e = e; }

        public sealed record Resultado(Guid Id, string Nombre, string ZonaObjetivo, string MusculoPrincipal, string Equipamiento);

        private async Task<List<Resultado>> BuscarAsync(Usuario quien, object cuerpo, HttpStatusCode esperado = HttpStatusCode.OK)
        {
            var r = await _e.Api.ClienteComo(quien).PostAsJsonAsync("/api/Ejercicios/busqueda-inteligente", cuerpo);
            Assert.True(r.StatusCode == esperado, $"{(int)r.StatusCode}: {await r.Content.ReadAsStringAsync()}");
            return esperado == HttpStatusCode.OK ? (await r.Content.ReadFromJsonAsync<List<Resultado>>())! : new();
        }

        private void SinIntrusos(IEnumerable<Resultado> l)
        {
            Assert.DoesNotContain(l, x => x.Id == _e.Inactivo || x.Id == _e.DeOtroGimnasio || x.Id == _e.ConBarra);
        }

        [Fact]
        public async Task El_Dueno_nunca_recibe_inactivos_ajenos_ni_sin_equipamiento_aunque_sean_los_mas_parecidos()
        {
            var l = await BuscarAsync(_e.Dueno1, new { consulta = "sentadilla", cantidad = 50 });
            SinIntrusos(l);
            Assert.Equal(_e.Valido1, l[0].Id);
            Assert.Contains(l, x => x.Id == _e.ValidoPesoCorporal);
            Assert.Contains(l, x => x.Id == _e.PropioG1);
        }

        [Fact]
        public async Task El_Socio_y_el_trainer_usan_el_gimnasio_de_su_ficha_o_sesion()
        {
            SinIntrusos(await BuscarAsync(_e.SocioG1, new { consulta = "sentadilla", cantidad = 50 }));
            SinIntrusos(await BuscarAsync(_e.Trainer1, new { consulta = "sentadilla", cantidad = 50 }));
        }

        [Fact]
        public async Task La_plataforma_con_gimnasio_aplica_sus_filtros_y_sin_gimnasio_ve_solo_globales()
        {
            var conG1 = await BuscarAsync(_e.Plataforma, new { consulta = "sentadilla", cantidad = 50, gimnasioId = _e.G1.Id });
            SinIntrusos(conG1);
            Assert.Contains(conG1, x => x.Id == _e.PropioG1);

            var globales = await BuscarAsync(_e.Plataforma, new { consulta = "sentadilla", cantidad = 50 });
            Assert.DoesNotContain(globales, x => x.Id == _e.Inactivo || x.Id == _e.DeOtroGimnasio || x.Id == _e.PropioG1);
            // Sin gimnasio de referencia no hay equipamiento declarado contra el que filtrar.
            Assert.Contains(globales, x => x.Id == _e.ConBarra);
        }

        [Fact]
        public async Task Zona_y_equipamiento_opcionales_restringen_el_resultado()
        {
            var espalda = await BuscarAsync(_e.Dueno1, new { consulta = "remo", cantidad = 50, zona = "back" });
            Assert.Equal(new[] { _e.ValidoEspalda }, espalda.Select(x => x.Id));

            var mancuerna = await BuscarAsync(_e.Dueno1, new { consulta = "sentadilla", cantidad = 50, equipamiento = new[] { "mancuerna" } });
            Assert.DoesNotContain(mancuerna, x => x.Id == _e.ValidoPesoCorporal);
            Assert.Contains(mancuerna, x => x.Id == _e.Valido1);
            SinIntrusos(mancuerna);
        }

        [Fact]
        public async Task El_GET_anterior_sigue_respondiendo_y_aplica_los_mismos_filtros()
        {
            var r = await _e.Api.ClienteComo(_e.Dueno1).GetAsync("/api/Ejercicios/busqueda-inteligente?consulta=sentadilla&cantidad=50");
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            var l = (await r.Content.ReadFromJsonAsync<List<Resultado>>())!;
            SinIntrusos(l);
            Assert.Equal(_e.Valido1, l[0].Id);
        }

        [Theory]
        [InlineData("", 5)]
        [InlineData("sentadilla", 0)]
        [InlineData("sentadilla", 51)]
        public async Task Pedidos_invalidos_responden_400_sin_llamar_a_la_IA(string consulta, int cantidad)
        {
            var antes = _e.Api.Ia.Llamadas;
            await BuscarAsync(_e.Dueno1, new { consulta, cantidad }, HttpStatusCode.BadRequest);
            Assert.Equal(antes, _e.Api.Ia.Llamadas);
        }

        [Fact]
        public async Task Otro_gimnasio_indicado_por_el_Dueno_responde_404()
        {
            await BuscarAsync(_e.Dueno1, new { consulta = "sentadilla", gimnasioId = _e.G2.Id }, HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Con_la_IA_caida_responde_503_con_mensaje_claro()
        {
            lock (_e)
            {
                _e.Api.Ia.Caida = true;
            }
            try
            {
                var r = await _e.Api.ClienteComo(_e.Dueno1).PostAsJsonAsync("/api/Ejercicios/busqueda-inteligente", new { consulta = "sentadilla" });
                Assert.Equal(HttpStatusCode.ServiceUnavailable, r.StatusCode);
                Assert.Contains("no está disponible", await r.Content.ReadAsStringAsync());
            }
            finally
            {
                _e.Api.Ia.Caida = false;
            }
        }

        public class Escenario : IAsyncLifetime
        {
            public ApiConIaBusqueda Api { get; } = new();
            public Gimnasio G1 { get; } = new() { Nombre = "Busqueda 1", Codigo = "BUSQ-1" };
            public Gimnasio G2 { get; } = new() { Nombre = "Busqueda 2", Codigo = "BUSQ-2" };
            public Guid Inactivo { get; } = Guid.NewGuid();
            public Guid DeOtroGimnasio { get; } = Guid.NewGuid();
            public Guid ConBarra { get; } = Guid.NewGuid();
            public Guid Valido1 { get; } = Guid.NewGuid();
            public Guid ValidoPesoCorporal { get; } = Guid.NewGuid();
            public Guid ValidoEspalda { get; } = Guid.NewGuid();
            public Guid PropioG1 { get; } = Guid.NewGuid();
            public Usuario Dueno1 { get; private set; } = null!;
            public Usuario Trainer1 { get; private set; } = null!;
            public Usuario SocioG1 { get; private set; } = null!;
            public Usuario Plataforma { get; private set; } = null!;

            public async Task InitializeAsync()
            {
                await Api.InitializeAsync();
                var socio = new Socio
                {
                    Id = Guid.NewGuid(), Nombre = "Sofia", Apellido = "Sintetica", Email = "sofia@busqueda.test", Genero = "F",
                    FechaNacimiento = new DateTime(1995, 1, 1, 0, 0, 0, DateTimeKind.Utc), GimnasioId = G1.Id,
                    Objetivo = ObjetivoEntrenamiento.Mantenimiento, Nivel = NivelExperiencia.Intermedio,
                };
                Dueno1 = new Usuario { Email = "dueno@busqueda.test", NombreCompleto = "Dueno", Rol = RolUsuario.AdministradorGimnasio, GimnasioId = G1.Id, PasswordHash = "-" };
                Trainer1 = new Usuario { Email = "trainer@busqueda.test", NombreCompleto = "Trainer", Rol = RolUsuario.Entrenador, GimnasioId = G1.Id, PasswordHash = "-" };
                SocioG1 = new Usuario { Email = socio.Email, NombreCompleto = "Sofia", Rol = RolUsuario.Socio, SocioId = socio.Id, PasswordHash = "-" };
                Plataforma = new Usuario { Email = "plataforma@busqueda.test", NombreCompleto = "Plataforma", Rol = RolUsuario.SuperAdministrador, PasswordHash = "-" };

                var mancuerna = CatalogoEquipamiento.IdDe(CatalogoEquipamiento.Mancuerna);
                var barra = CatalogoEquipamiento.IdDe(CatalogoEquipamiento.Barra);
                var pesoCorporal = CatalogoEquipamiento.IdDe(CatalogoEquipamiento.PesoCorporal);

                Ejercicio Ej(Guid id, string nombre, string zona, Guid tipo, float segundo, bool activo = true, Guid? gimnasio = null) => new()
                {
                    Id = id, Nombre = nombre, ZonaObjetivo = zona, TipoEquipamientoId = tipo, Activo = activo, GimnasioId = gimnasio,
                    Origen = gimnasio is null ? OrigenEjercicio.Proyecto : OrigenEjercicio.Gimnasio,
                    Embedding = new Vector(IaBusquedaFalsa.Base(segundo)),
                };

                await Api.ConDbAsync(db =>
                {
                    db.Gimnasios.AddRange(G1, G2);
                    db.Socios.Add(socio);
                    db.Usuarios.AddRange(Dueno1, Trainer1, SocioG1, Plataforma);
                    db.EquipamientosGimnasio.Add(new EquipamientoGimnasio { GimnasioId = G1.Id, TipoEquipamientoId = mancuerna });
                    db.Ejercicios.AddRange(
                        Ej(Inactivo, "Intruso inactivo", "Legs", mancuerna, 0f, activo: false),
                        Ej(DeOtroGimnasio, "Intruso de G2", "Legs", mancuerna, 0f, gimnasio: G2.Id),
                        Ej(ConBarra, "Intruso con barra", "Legs", barra, 0f),
                        Ej(Valido1, "Valido mancuerna", "Legs", mancuerna, 0.05f),
                        Ej(ValidoPesoCorporal, "Valido peso corporal", "Legs", pesoCorporal, 0.3f),
                        Ej(ValidoEspalda, "Valido espalda", "Back", mancuerna, 0.4f),
                        Ej(PropioG1, "Propio de G1", "Legs", mancuerna, 0.2f, gimnasio: G1.Id));
                    return Task.CompletedTask;
                });
            }

            public async Task DisposeAsync() => await Api.DisposeAsync();
        }
    }
}
