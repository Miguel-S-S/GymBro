using System.Net;
using System.Net.Http.Json;
using GymBro.Application.Services;
using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Domain.Rules;
using GymBro.Infrastructure.Persistence.Seeders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace GymBro.Tests.Integration
{
    /// <summary>Servicio de IA simulado (Etapa 9a-2): éxito, error o demora mayor al tiempo máximo configurado.</summary>
    public class IaFalsa : IIaService
    {
        public enum ModoIa { Ok, Falla, Demora }
        public ModoIa Modo { get; set; } = ModoIa.Ok;
        public List<string> Textos { get; } = new();

        public static float[] Vector() => Enumerable.Range(0, EmbeddingConfig.Dimension).Select(i => i == 0 ? 1f : 0.01f).ToArray();

        public async Task<float[]> GenerarEmbeddingAsync(string texto)
        {
            lock (Textos) Textos.Add(texto);
            switch (Modo)
            {
                case ModoIa.Falla: throw new HttpRequestException("Servicio de IA no disponible (simulado).");
                case ModoIa.Demora: await Task.Delay(TimeSpan.FromSeconds(4)); return Vector();
                default: return Vector();
            }
        }
    }

    /// <summary>API con la IA simulada y un tiempo máximo de 1 s para el embedding.</summary>
    public class ApiConIaFalsa : GymBroApiFactory
    {
        public IaFalsa Ia { get; } = new();

        protected override void ConfigurarExtra(IWebHostBuilder builder)
        {
            builder.UseSetting("Embeddings:TiempoMaximoSegundos", "1");
            builder.ConfigureTestServices(s =>
            {
                s.RemoveAll<IIaService>();
                s.AddSingleton<IIaService>(Ia);
            });
        }
    }

    /// <summary>
    /// Etapa 9a-2: ejercicios y equipamiento propios del gimnasio, contra la API real y PostgreSQL descartable.
    /// Escenario SINTÉTICO de prueba (sin datos reales):
    ///   G1: Dueño, Personal Trainer, socios A y A2 (con cuenta). G2: Dueño, socio con cuenta y dos ejercicios propios.
    ///   G3 y G4: Dueño y socio con cuenta (para el baseline con equipamiento). Socio independiente (sin gimnasio).
    ///   Catálogo global: Core (CatalogoProyectoSeeder) y dos ejercicios de Pecho (polea y mancuerna).
    /// </summary>
    public class CatalogoGimnasioTests : IClassFixture<CatalogoGimnasioTests.Escenario>
    {
        public sealed record ItemCatalogo(Guid Id, string Nombre, string ZonaObjetivo, string Equipamiento, string Origen,
            Guid? GimnasioId, Guid? TipoEquipamientoId, string? TipoEquipamiento, bool Activo = true);
        public sealed record Pagina(List<ItemCatalogo> Items, int Total);
        public sealed record Buscado(Guid Id, string Nombre);
        public sealed record ItemEquipamiento(Guid TipoEquipamientoId, string Clave, string Nombre, bool SiempreDisponible, bool Propio, bool Marcado, int? Cantidad);
        public sealed record Equipamiento(Guid GimnasioId, string NombreGimnasio, bool Editable, List<ItemEquipamiento> Items);
        public sealed record EjercicioRutina(Guid EjercicioId, string Ejercicio);
        public sealed record Rutina(Guid Id, string? Observaciones, List<EjercicioRutina> Ejercicios);

        private static readonly Guid Plancha = Guid.Parse("13e90ee5-9fb4-47bf-9abc-1277eae966d1");
        private static readonly Guid TipoPesoCorporal = CatalogoEquipamiento.IdDe(CatalogoEquipamiento.PesoCorporal);
        private static readonly Guid TipoMancuerna = CatalogoEquipamiento.IdDe(CatalogoEquipamiento.Mancuerna);
        private static readonly Guid TipoBarra = CatalogoEquipamiento.IdDe(CatalogoEquipamiento.Barra);
        private static readonly Guid TipoPolea = CatalogoEquipamiento.IdDe(CatalogoEquipamiento.Polea);

        private readonly Escenario _e;
        public CatalogoGimnasioTests(Escenario e) { _e = e; }

        private HttpClient Como(Usuario u) => _e.Api.ClienteComo(u);

        private static object Pedido(string nombre, Guid? tipo = null, string zona = "Core", int dificultad = 2,
            string ejecucion = "Sostener la posición indicada.", string medida = "Repeticiones") => new
            {
                nombre, zonaObjetivo = zona, tipoEquipamientoId = tipo ?? TipoPesoCorporal, musculoPrincipal = "Recto abdominal",
                preparacion = "Colocarse en posición.", ejecucion, tipoMedida = medida, dificultad
            };

        private async Task<ItemCatalogo> CrearAsync(Usuario dueno, string nombre, Guid? tipo = null)
        {
            var r = await Como(dueno).PostAsJsonAsync("/api/Ejercicios", Pedido(nombre, tipo));
            Assert.True(r.StatusCode == HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
            return (await r.Content.ReadFromJsonAsync<ItemCatalogo>())!;
        }

        private async Task<List<ItemCatalogo>> ListarAsync(Usuario u, string query = "origen=Gimnasio")
        {
            var p = await Como(u).GetFromJsonAsync<Pagina>($"/api/Ejercicios?tamanioPagina=100&{query}");
            return p!.Items;
        }

        private async Task<bool> TieneEmbeddingAsync(Guid id)
        {
            var tiene = false;
            await _e.Api.ConDbAsync(async db => tiene = await db.Ejercicios.IgnoreQueryFilters().AnyAsync(x => x.Id == id && x.Embedding != null));
            return tiene;
        }

        // ---- Alta, origen y aislamiento ----

        [Fact]
        public async Task El_Dueno_crea_un_ejercicio_propio_y_el_servidor_fija_origen_gimnasio_y_embedding()
        {
            // El cuerpo intenta fijar el origen: se ignora. Etapa 9a-2b: indicar otro gimnasio ya no se ignora, responde 404
            // (lo cubre AdministracionCatalogoTests); acá se envía el propio.
            var cuerpo = new
            {
                nombre = "Plancha con toalla", zonaObjetivo = "core", tipoEquipamientoId = TipoPesoCorporal,
                musculoPrincipal = "Recto abdominal", ejecucion = "Sostener.", tipoMedida = "segundos", dificultad = 2,
                origen = "Dataset", gimnasioId = _e.G1.Id
            };
            var r = await Como(_e.Dueno1).PostAsJsonAsync("/api/Ejercicios", cuerpo);
            Assert.Equal(HttpStatusCode.Created, r.StatusCode);
            var creado = (await r.Content.ReadFromJsonAsync<ItemCatalogo>())!;

            Assert.Equal(("Gimnasio", _e.G1.Id, "Core"), (creado.Origen, creado.GimnasioId, creado.ZonaObjetivo));
            Assert.Equal(("Peso corporal", "Peso corporal", TipoPesoCorporal), (creado.Equipamiento, creado.TipoEquipamiento, creado.TipoEquipamientoId));
            Assert.True(await TieneEmbeddingAsync(creado.Id));
            Assert.Contains("Ejercicio: Plancha con toalla. Zona objetivo: Core. Músculo principal: Recto abdominal. Técnica y ejecución: Sostener.", _e.Api.Ia.Textos);
        }

        [Fact]
        public async Task Un_ejercicio_propio_de_A_no_aparece_para_B_en_listado_detalle_zonas_ni_busqueda()
        {
            var propio = await CrearAsync(_e.Dueno1, "Aislamiento G1");

            // Dueño de otro gimnasio: listado, detalle (404), zonas y búsqueda inteligente.
            Assert.DoesNotContain(await ListarAsync(_e.Dueno2), i => i.Id == propio.Id);
            Assert.Equal(HttpStatusCode.NotFound, (await Como(_e.Dueno2).GetAsync($"/api/Ejercicios/{propio.Id}")).StatusCode);
            var buscadosB = await Como(_e.Dueno2).GetAsync("/api/Ejercicios/busqueda-inteligente?consulta=plancha&cantidad=50");
            if (buscadosB.IsSuccessStatusCode)
                Assert.DoesNotContain((await buscadosB.Content.ReadFromJsonAsync<List<Buscado>>())!, b => b.Id == propio.Id);
            else Assert.Equal(HttpStatusCode.NotFound, buscadosB.StatusCode);

            var zonasG1 = await Como(_e.Dueno1).GetFromJsonAsync<List<string>>("/api/Ejercicios/zonas");
            var zonasG2 = await Como(_e.Dueno2).GetFromJsonAsync<List<string>>("/api/Ejercicios/zonas");
            Assert.DoesNotContain("ZonaPrivadaG2", zonasG1!);
            Assert.Contains("ZonaPrivadaG2", zonasG2!);

            // El propio gimnasio (Dueño, Personal Trainer y socio vinculado) sí lo ve, también en la búsqueda.
            Assert.Contains(await ListarAsync(_e.Trainer1), i => i.Id == propio.Id);
            Assert.Contains(await ListarAsync(_e.UsuarioSocioA), i => i.Id == propio.Id);
            Assert.Equal(HttpStatusCode.OK, (await Como(_e.UsuarioSocioA).GetAsync($"/api/Ejercicios/{propio.Id}")).StatusCode);
            var buscadosA = await Como(_e.Dueno1).GetFromJsonAsync<List<Buscado>>("/api/Ejercicios/busqueda-inteligente?consulta=plancha&cantidad=50");
            Assert.Contains(buscadosA!, b => b.Id == propio.Id);
        }

        [Fact]
        public async Task El_Administrador_de_la_Plataforma_ve_los_ejercicios_de_todos_y_el_socio_independiente_solo_los_globales()
        {
            var propio = await CrearAsync(_e.Dueno1, "Visible para la plataforma");

            var plataforma = await ListarAsync(_e.SuperAdmin);
            Assert.Contains(plataforma, i => i.Id == propio.Id);
            Assert.Contains(plataforma, i => i.GimnasioId == _e.G2.Id);
            Assert.Equal(HttpStatusCode.OK, (await Como(_e.SuperAdmin).GetAsync($"/api/Ejercicios/{_e.PropioG2}")).StatusCode);

            Assert.Empty(await ListarAsync(_e.UsuarioSocioIndependiente));
            Assert.Contains(await ListarAsync(_e.UsuarioSocioIndependiente, "zona=Core"), i => i.Id == Plancha);

            var socioG2 = await ListarAsync(_e.UsuarioSocioG2);
            Assert.All(socioG2, i => Assert.Equal(_e.G2.Id, i.GimnasioId));
            Assert.Contains(socioG2, i => i.Id == _e.PropioG2);
        }

        [Fact]
        public async Task El_Dueno_no_puede_tocar_un_global_403_ni_uno_de_otro_gimnasio_404()
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await Como(_e.Dueno1).PutAsJsonAsync($"/api/Ejercicios/{Plancha}", Pedido("Plancha editada"))).StatusCode);
            // Etapa 9a-2b: la baja se reemplazó por desactivar (PUT {id}/estado).
            Assert.Equal(HttpStatusCode.Forbidden, (await Como(_e.Dueno1).PutAsJsonAsync($"/api/Ejercicios/{Plancha}/estado", new { activo = false })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Como(_e.Dueno1).PutAsJsonAsync($"/api/Ejercicios/{_e.PropioG2}", Pedido("Ajeno"))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Como(_e.Dueno1).PutAsJsonAsync($"/api/Ejercicios/{_e.PropioG2}/estado", new { activo = false })).StatusCode);
        }

        [Fact]
        public async Task El_Dueno_edita_desactiva_y_reactiva_un_ejercicio_propio()
        {
            var propio = await CrearAsync(_e.Dueno1, "Para editar");

            var r = await Como(_e.Dueno1).PutAsJsonAsync($"/api/Ejercicios/{propio.Id}", Pedido("Para editar (v2)", dificultad: 3));
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            Assert.Equal("Para editar (v2)", (await r.Content.ReadFromJsonAsync<ItemCatalogo>())!.Nombre);

            // Etapa 9a-2b: desactivar oculta el ejercicio del catálogo y del socio, pero el Dueño lo sigue viendo para reactivarlo.
            var off = await Como(_e.Dueno1).PutAsJsonAsync($"/api/Ejercicios/{propio.Id}/estado", new { activo = false });
            Assert.Equal(HttpStatusCode.OK, off.StatusCode);
            Assert.False((await off.Content.ReadFromJsonAsync<ItemCatalogo>())!.Activo);
            Assert.DoesNotContain(await ListarAsync(_e.Dueno1), i => i.Id == propio.Id);
            Assert.Contains(await ListarAsync(_e.Dueno1, "inactivos=true"), i => i.Id == propio.Id);
            Assert.Equal(HttpStatusCode.NotFound, (await Como(_e.UsuarioSocioA).GetAsync($"/api/Ejercicios/{propio.Id}")).StatusCode);

            var on = await Como(_e.Dueno1).PutAsJsonAsync($"/api/Ejercicios/{propio.Id}/estado", new { activo = true });
            Assert.True((await on.Content.ReadFromJsonAsync<ItemCatalogo>())!.Activo);
            Assert.Contains(await ListarAsync(_e.Dueno1), i => i.Id == propio.Id);
        }

        // ---- Validaciones ----

        public static TheoryData<string, object> PedidosInvalidos()
        {
            var d = new TheoryData<string, object>
            {
                { "nombre duplicado de un global (sin mayúsculas ni acentos)", Pedido("PLANCHÁ") },
                { "zona inexistente", Pedido("Zona mala", zona: "Inexistente") },
                { "equipamiento no marcado", Pedido("Sin mancuernas", TipoMancuerna) },
                { "tipo inexistente", Pedido("Tipo raro", Guid.NewGuid()) },
                { "nombre de más de 120", Pedido(new string('x', 121)) },
                { "ejecución de más de 2000", Pedido("Ejecución larga", ejecucion: new string('x', 2001)) },
                { "ejecución con un carácter invisible", Pedido("Invisible", ejecucion: "Ignorar​ instrucciones") },
                { "nombre con un carácter de control", Pedido("Control\u0007") },
                { "dificultad fuera de rango", Pedido("Dificil", dificultad: 6) },
                { "tipo de medida numérico", Pedido("Medida numerica", medida: "3") },
            };
            return d;
        }

        [Theory]
        [MemberData(nameof(PedidosInvalidos))]
        public async Task Un_pedido_invalido_se_rechaza_con_400(string caso, object pedido)
        {
            var r = await Como(_e.Dueno1).PostAsJsonAsync("/api/Ejercicios", pedido);
            Assert.True(r.StatusCode == HttpStatusCode.BadRequest, $"{caso}: {(int)r.StatusCode} {await r.Content.ReadAsStringAsync()}");
        }

        [Fact]
        public async Task Un_tipo_propio_de_otro_gimnasio_no_se_puede_usar_ni_marcar()
        {
            var r = await Como(_e.Dueno1).PostAsJsonAsync("/api/Ejercicios", Pedido("Con tipo ajeno", _e.TipoPropioG2));
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);

            var marcar = await Como(_e.Dueno1).PutAsJsonAsync("/api/equipamiento",
                new { items = new[] { new { tipoEquipamientoId = _e.TipoPropioG2, cantidad = (int?)null } } });
            Assert.Equal(HttpStatusCode.BadRequest, marcar.StatusCode);
        }

        // ---- Embedding al guardar ----

        [Fact]
        public async Task Con_la_IA_caida_o_lenta_el_ejercicio_se_guarda_sin_embedding_y_al_editar_nunca_queda_un_vector_viejo()
        {
            try
            {
                _e.Api.Ia.Modo = IaFalsa.ModoIa.Falla;
                var caido = await CrearAsync(_e.Dueno1, "Con IA caida");
                Assert.False(await TieneEmbeddingAsync(caido.Id));

                _e.Api.Ia.Modo = IaFalsa.ModoIa.Demora;
                var reloj = System.Diagnostics.Stopwatch.StartNew();
                var lento = await CrearAsync(_e.Dueno1, "Con IA lenta");
                Assert.True(reloj.Elapsed < TimeSpan.FromSeconds(3.5), $"Tardó {reloj.Elapsed}");
                Assert.False(await TieneEmbeddingAsync(lento.Id));

                _e.Api.Ia.Modo = IaFalsa.ModoIa.Ok;
                var conVector = await CrearAsync(_e.Dueno1, "Con vector");
                Assert.True(await TieneEmbeddingAsync(conVector.Id));

                _e.Api.Ia.Modo = IaFalsa.ModoIa.Falla;
                var r = await Como(_e.Dueno1).PutAsJsonAsync($"/api/Ejercicios/{conVector.Id}", Pedido("Con vector renombrado"));
                Assert.Equal(HttpStatusCode.OK, r.StatusCode);
                Assert.False(await TieneEmbeddingAsync(conVector.Id));
            }
            finally { _e.Api.Ia.Modo = IaFalsa.ModoIa.Ok; }
        }

        // ---- Baja e historial ----

        [Fact]
        public async Task Desactivar_un_ejercicio_en_una_rutina_vigente_se_rechaza()
        {
            var propio = await CrearAsync(_e.Dueno1, "En rutina vigente");
            await _e.AgregarRutinaAsync(_e.SocioA.Id, propio.Id, EstadoPrescripcion.Aprobada);

            var r = await Como(_e.Dueno1).PutAsJsonAsync($"/api/Ejercicios/{propio.Id}/estado", new { activo = false });
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
            Assert.Contains("rutina vigente", await r.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task El_historial_conserva_el_nombre_tras_desactivar_y_tras_dejar_el_gimnasio()
        {
            var dadoDeBaja = await CrearAsync(_e.Dueno1, "Historial baja");
            await _e.AgregarRutinaAsync(_e.SocioA.Id, dadoDeBaja.Id, EstadoPrescripcion.Rechazada);
            Assert.Equal(HttpStatusCode.OK, (await Como(_e.Dueno1).PutAsJsonAsync($"/api/Ejercicios/{dadoDeBaja.Id}/estado", new { activo = false })).StatusCode);

            var rutinasA = await Como(_e.UsuarioSocioA).GetFromJsonAsync<List<Rutina>>($"/api/Socio/{_e.SocioA.Id}/prescripciones");
            Assert.Contains(rutinasA!.SelectMany(r => r.Ejercicios), e => e.EjercicioId == dadoDeBaja.Id && e.Ejercicio == "Historial baja");
            var revision = await Como(_e.Dueno1).GetFromJsonAsync<List<Rutina>>($"/api/Socio/{_e.SocioA.Id}/prescripciones");
            Assert.Contains(revision!.SelectMany(r => r.Ejercicios), e => e.Ejercicio == "Historial baja");

            // El socio A2 deja el gimnasio: deja de ver los ejercicios propios, pero su historial conserva el nombre.
            var delGimnasio = await CrearAsync(_e.Dueno1, "Historial ex gimnasio");
            await _e.AgregarRutinaAsync(_e.SocioA2.Id, delGimnasio.Id, EstadoPrescripcion.Cancelada);
            await _e.Api.ConDbAsync(async db => (await db.Socios.SingleAsync(s => s.Id == _e.SocioA2.Id)).GimnasioId = null);

            Assert.DoesNotContain(await ListarAsync(_e.UsuarioSocioA2), i => i.Id == delGimnasio.Id);
            var rutinasA2 = await Como(_e.UsuarioSocioA2).GetFromJsonAsync<List<Rutina>>($"/api/Socio/{_e.SocioA2.Id}/prescripciones");
            Assert.Contains(rutinasA2!.SelectMany(r => r.Ejercicios), e => e.EjercicioId == delGimnasio.Id && e.Ejercicio == "Historial ex gimnasio");
        }

        // ---- Equipamiento ----

        [Fact]
        public async Task El_Dueno_marca_equipamiento_y_crea_un_tipo_propio_aislado()
        {
            var inicial = await Como(_e.Dueno2).GetFromJsonAsync<Equipamiento>("/api/equipamiento");
            Assert.True(inicial!.Editable);
            Assert.True(inicial.Items.Count >= 13);
            Assert.All(inicial.Items.Where(i => i.SiempreDisponible), i => Assert.True(i.Marcado));

            var nuevo = await Como(_e.Dueno2).PostAsJsonAsync("/api/equipamiento/tipos", new { nombre = "Kettlebell  rusa" });
            Assert.Equal(HttpStatusCode.Created, nuevo.StatusCode);
            var tipo = (await nuevo.Content.ReadFromJsonAsync<ItemEquipamiento>())!;
            Assert.Equal(("Kettlebell rusa", "GIM_KETTLEBELL_RUSA", true, false), (tipo.Nombre, tipo.Clave, tipo.Propio, tipo.Marcado));
            Assert.Equal(HttpStatusCode.BadRequest, (await Como(_e.Dueno2).PostAsJsonAsync("/api/equipamiento/tipos", new { nombre = "KETTLEBELL RUSA" })).StatusCode);

            var marcar = await Como(_e.Dueno2).PutAsJsonAsync("/api/equipamiento", new
            {
                items = new object[] { new { tipoEquipamientoId = TipoMancuerna, cantidad = 10 }, new { tipoEquipamientoId = tipo.TipoEquipamientoId, cantidad = (int?)null } }
            });
            Assert.Equal(HttpStatusCode.OK, marcar.StatusCode);
            var marcado = (await marcar.Content.ReadFromJsonAsync<Equipamiento>())!;
            Assert.Equal(10, marcado.Items.Single(i => i.TipoEquipamientoId == TipoMancuerna).Cantidad);
            Assert.True(marcado.Items.Single(i => i.TipoEquipamientoId == tipo.TipoEquipamientoId).Marcado);

            // Quitar uno (baja lógica) y volver a marcarlo: un único registro vigente.
            await Como(_e.Dueno2).PutAsJsonAsync("/api/equipamiento", new { items = new[] { new { tipoEquipamientoId = tipo.TipoEquipamientoId } } });
            var final = await Como(_e.Dueno2).PutAsJsonAsync("/api/equipamiento", new { items = new[] { new { tipoEquipamientoId = TipoMancuerna } } });
            Assert.False((await final.Content.ReadFromJsonAsync<Equipamiento>())!.Items.Single(i => i.TipoEquipamientoId == tipo.TipoEquipamientoId).Marcado);
            var filas = 0;
            await _e.Api.ConDbAsync(async db => filas = await db.EquipamientosGimnasio.IgnoreQueryFilters()
                .CountAsync(x => x.GimnasioId == _e.G2.Id && x.TipoEquipamientoId == TipoMancuerna && x.FechaBaja == null));
            Assert.Equal(1, filas);

            // El tipo propio queda aislado: otro Dueño no lo ve.
            var otro = await Como(_e.Dueno1).GetFromJsonAsync<Equipamiento>("/api/equipamiento");
            Assert.DoesNotContain(otro!.Items, i => i.TipoEquipamientoId == tipo.TipoEquipamientoId);

            // Cantidad inválida.
            Assert.Equal(HttpStatusCode.BadRequest, (await Como(_e.Dueno2).PutAsJsonAsync("/api/equipamiento",
                new { items = new[] { new { tipoEquipamientoId = TipoBarra, cantidad = 0 } } })).StatusCode);
        }

        [Fact]
        public async Task El_Administrador_de_la_Plataforma_lee_el_equipamiento_de_cualquier_gimnasio()
        {
            // Etapa 9a-2b (D2): además lo puede editar (lo cubre AdministracionCatalogoTests).
            var g2 = await Como(_e.SuperAdmin).GetFromJsonAsync<Equipamiento>($"/api/equipamiento?gimnasioId={_e.G2.Id}");
            Assert.True(g2!.Editable);
            Assert.Contains(g2.Items, i => i.TipoEquipamientoId == _e.TipoPropioG2);
            Assert.Equal(HttpStatusCode.BadRequest, (await Como(_e.SuperAdmin).GetAsync("/api/equipamiento")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Como(_e.SuperAdmin).GetAsync($"/api/equipamiento?gimnasioId={Guid.NewGuid()}")).StatusCode);

            var trainer = await Como(_e.Trainer1).GetFromJsonAsync<Equipamiento>("/api/equipamiento");
            Assert.Equal((_e.G1.Id, false), (trainer!.GimnasioId, trainer.Editable));
            Assert.Equal(HttpStatusCode.NotFound, (await Como(_e.Dueno1).GetAsync($"/api/equipamiento?gimnasioId={_e.G2.Id}")).StatusCode);
        }

        // ---- Baseline ----

        [Fact]
        public async Task Baseline_con_equipamiento_declarado_elige_solo_factibles_con_el_gimnasio_del_socio()
        {
            // G3 declara mancuernas; el baseline lo dispara el propio socio (su cuenta no lleva gimnasio_id).
            var marcar = await Como(_e.Dueno3).PutAsJsonAsync("/api/equipamiento", new { items = new[] { new { tipoEquipamientoId = TipoMancuerna } } });
            Assert.Equal(HttpStatusCode.OK, marcar.StatusCode);

            var rutina = await _e.GenerarBaselineAsync(_e.UsuarioSocioG3, _e.SocioG3.Id);

            Assert.Contains(rutina.Ejercicios, e => e.EjercicioId == _e.PechoMancuerna);
            Assert.DoesNotContain(rutina.Ejercicios, e => e.EjercicioId == _e.PechoPolea || e.EjercicioId == _e.PropioG2);
            Assert.Contains(rutina.Ejercicios, e => e.Ejercicio == "Crunch abdominal"); // peso corporal: siempre disponible
            Assert.Contains("Se usó el equipamiento declarado por el gimnasio.", rutina.Observaciones);
        }

        [Fact]
        public async Task Baseline_con_una_zona_sin_equipamiento_la_omite_y_lo_dice()
        {
            await Como(_e.Dueno4).PutAsJsonAsync("/api/equipamiento", new { items = new[] { new { tipoEquipamientoId = TipoBarra } } });

            var rutina = await _e.GenerarBaselineAsync(_e.Dueno4, _e.SocioG4.Id);

            Assert.DoesNotContain(rutina.Ejercicios, e => e.EjercicioId == _e.PechoMancuerna || e.EjercicioId == _e.PechoPolea);
            Assert.Contains("Zonas omitidas por equipamiento (el gimnasio no declaró el necesario): Pecho", rutina.Observaciones);
            Assert.True(rutina.Observaciones!.Length <= 500);
        }

        [Fact]
        public async Task Baseline_sin_equipamiento_declarado_o_socio_independiente_no_filtra_y_nunca_usa_ejercicios_de_otro_gimnasio()
        {
            var independiente = await _e.GenerarBaselineAsync(_e.UsuarioSocioIndependiente, _e.SocioIndependiente.Id);
            Assert.Contains(independiente.Ejercicios, e => e.EjercicioId == _e.PechoPolea); // primero por dificultad y nombre
            Assert.DoesNotContain(independiente.Ejercicios, e => e.EjercicioId == _e.PropioG2);
            Assert.DoesNotContain("equipamiento", independiente.Observaciones);

            // El socio de G2 sí recibe el ejercicio propio de su gimnasio (G2 no declaró equipamiento en este punto
            // o, si otra prueba lo declaró, el propio es de peso corporal y sigue siendo factible).
            var socioG2 = await _e.GenerarBaselineAsync(_e.UsuarioSocioG2, _e.SocioG2.Id);
            Assert.Contains(socioG2.Ejercicios, e => e.EjercicioId == _e.PropioG2);
        }

        // ---- Escenario ----

        public class Escenario : IAsyncLifetime
        {
            public ApiConIaFalsa Api { get; } = new();
            public Gimnasio G1 { get; } = new() { Nombre = "Gimnasio 1 (9a-2)", Codigo = "EQ-1", RequiereAprobacionRutinas = false };
            public Gimnasio G2 { get; } = new() { Nombre = "Gimnasio 2 (9a-2)", Codigo = "EQ-2", RequiereAprobacionRutinas = false };
            public Gimnasio G3 { get; } = new() { Nombre = "Gimnasio 3 (9a-2)", Codigo = "EQ-3", RequiereAprobacionRutinas = false };
            public Gimnasio G4 { get; } = new() { Nombre = "Gimnasio 4 (9a-2)", Codigo = "EQ-4", RequiereAprobacionRutinas = false };

            public Usuario Dueno1 { get; private set; } = null!;
            public Usuario Dueno2 { get; private set; } = null!;
            public Usuario Dueno3 { get; private set; } = null!;
            public Usuario Dueno4 { get; private set; } = null!;
            public Usuario Trainer1 { get; private set; } = null!;
            public Usuario SuperAdmin { get; private set; } = null!;
            public Socio SocioA { get; private set; } = null!;
            public Socio SocioA2 { get; private set; } = null!;
            public Socio SocioG2 { get; private set; } = null!;
            public Socio SocioG3 { get; private set; } = null!;
            public Socio SocioG4 { get; private set; } = null!;
            public Socio SocioIndependiente { get; private set; } = null!;
            public Usuario UsuarioSocioA { get; private set; } = null!;
            public Usuario UsuarioSocioA2 { get; private set; } = null!;
            public Usuario UsuarioSocioG2 { get; private set; } = null!;
            public Usuario UsuarioSocioG3 { get; private set; } = null!;
            public Usuario UsuarioSocioIndependiente { get; private set; } = null!;

            public Guid PechoPolea { get; } = Guid.NewGuid();
            public Guid PechoMancuerna { get; } = Guid.NewGuid();
            public Guid PropioG2 { get; } = Guid.NewGuid();
            public Guid TipoPropioG2 { get; } = Guid.NewGuid();

            public async Task InitializeAsync()
            {
                await Api.InitializeAsync();
                Dueno1 = Personal("dueno1@eq.test", RolUsuario.AdministradorGimnasio, G1.Id);
                Dueno2 = Personal("dueno2@eq.test", RolUsuario.AdministradorGimnasio, G2.Id);
                Dueno3 = Personal("dueno3@eq.test", RolUsuario.AdministradorGimnasio, G3.Id);
                Dueno4 = Personal("dueno4@eq.test", RolUsuario.AdministradorGimnasio, G4.Id);
                Trainer1 = Personal("trainer1@eq.test", RolUsuario.Entrenador, G1.Id);
                SuperAdmin = new Usuario { Email = "plataforma@eq.test", NombreCompleto = "Plataforma", Rol = RolUsuario.SuperAdministrador, PasswordHash = "-" };
                (SocioA, UsuarioSocioA) = NuevoSocio("a@eq.test", G1.Id);
                (SocioA2, UsuarioSocioA2) = NuevoSocio("a2@eq.test", G1.Id);
                (SocioG2, UsuarioSocioG2) = NuevoSocio("g2@eq.test", G2.Id);
                (SocioG3, UsuarioSocioG3) = NuevoSocio("g3@eq.test", G3.Id);
                (SocioG4, _) = NuevoSocio("g4@eq.test", G4.Id);
                (SocioIndependiente, UsuarioSocioIndependiente) = NuevoSocio("indep@eq.test", null);
                var socios = new[] { SocioA, SocioA2, SocioG2, SocioG3, SocioG4, SocioIndependiente };

                await Api.ConDbAsync(db =>
                {
                    db.Gimnasios.AddRange(G1, G2, G3, G4);
                    db.Socios.AddRange(socios);
                    db.Usuarios.AddRange(Dueno1, Dueno2, Dueno3, Dueno4, Trainer1, SuperAdmin,
                        UsuarioSocioA, UsuarioSocioA2, UsuarioSocioG2, UsuarioSocioG3, UsuarioSocioIndependiente);
                    db.MedicionesFisicas.AddRange(socios.Select(s => new MedicionFisica { SocioId = s.Id, PesoKg = 70, AlturaCm = 175, IMC = 22.9m }));
                    db.Ejercicios.AddRange(
                        Global(PechoPolea, "Pecho A polea", "Cable", TipoPolea),
                        Global(PechoMancuerna, "Pecho B mancuerna", "Dumbbell", CatalogoEquipamiento.IdDe(CatalogoEquipamiento.Mancuerna), dificultad: 2),
                        new Ejercicio
                        {
                            Id = PropioG2, Nombre = "AAA propio G2", Equipamiento = "Peso corporal", ZonaObjetivo = "Chest", MusculoPrincipal = "Pectoral",
                            Ejecucion = "-", Dificultad = 1, Origen = OrigenEjercicio.Gimnasio, GimnasioId = G2.Id, TipoEquipamientoId = TipoPesoCorporal,
                        },
                        new Ejercicio
                        {
                            Id = Guid.NewGuid(), Nombre = "Zona privada G2", Equipamiento = "Peso corporal", ZonaObjetivo = "ZonaPrivadaG2", MusculoPrincipal = "-",
                            Ejecucion = "-", Dificultad = 1, Origen = OrigenEjercicio.Gimnasio, GimnasioId = G2.Id, TipoEquipamientoId = TipoPesoCorporal,
                        });
                    db.Dosificaciones.Add(new DosificacionVolumetrica
                    {
                        Id = Guid.NewGuid(), ParteCuerpo = "Chest", SubzonaObjetivo = "General", ZonaObjetivo = "Chest",
                        SeriesMinimas = 3, SeriesMaximas = 4, RepeticionesMinimas = 8, RepeticionesMaximas = 12,
                    });
                    db.TiposEquipamiento.Add(new TipoEquipamiento { Id = TipoPropioG2, Clave = "GIM_BANCO_G2", Nombre = "Banco G2", GimnasioId = G2.Id });
                    return Task.CompletedTask;
                });
            }

            public async Task AgregarRutinaAsync(Guid socioId, Guid ejercicioId, EstadoPrescripcion estado) =>
                await Api.ConDbAsync(db =>
                {
                    var p = new PrescripcionEntrenamiento { SocioId = socioId, OrigenGeneracion = "PRUEBA", Estado = estado };
                    p.EjerciciosPrescritos.Add(new PrescripcionEjercicio
                    {
                        PrescripcionEntrenamientoId = p.Id, EjercicioId = ejercicioId, Orden = 1, SeriesObjetivo = 3,
                        RepeticionesMinimas = 8, RepeticionesMaximas = 12, RIRObjetivo = 2, DescansoObjetivoSegundos = 90,
                    });
                    db.PrescripcionesEntrenamiento.Add(p);
                    return Task.CompletedTask;
                });

            public async Task<Rutina> GenerarBaselineAsync(Usuario quien, Guid socioId)
            {
                var r = await Api.ClienteComo(quien).PostAsync($"/api/Socio/{socioId}/prescripciones/baseline", null);
                Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync());
                return (await r.Content.ReadFromJsonAsync<Rutina>())!;
            }

            public async Task DisposeAsync() => await Api.DisposeAsync();

            private static Usuario Personal(string email, RolUsuario rol, Guid gimnasioId) =>
                new() { Email = email, NombreCompleto = email, Rol = rol, GimnasioId = gimnasioId, PasswordHash = "-" };

            private static (Socio, Usuario) NuevoSocio(string email, Guid? gimnasioId)
            {
                var s = new Socio
                {
                    Id = Guid.NewGuid(), Nombre = "Socio", Apellido = "Prueba", Email = email, Genero = "F",
                    FechaNacimiento = new DateTime(1995, 1, 1, 0, 0, 0, DateTimeKind.Utc), GimnasioId = gimnasioId,
                    Objetivo = ObjetivoEntrenamiento.Mantenimiento, Nivel = NivelExperiencia.Principiante,
                    ConsentimientoDatosSaludFecha = DateTime.UtcNow,
                };
                return (s, new Usuario { Email = email, NombreCompleto = email, Rol = RolUsuario.Socio, SocioId = s.Id, PasswordHash = "-" });
            }

            private static Ejercicio Global(Guid id, string nombre, string equipamiento, Guid tipo, int dificultad = 1) => new()
            {
                Id = id, Nombre = nombre, Equipamiento = equipamiento, ZonaObjetivo = "Chest", MusculoPrincipal = "Pectoral",
                Ejecucion = "-", Dificultad = dificultad, TipoEquipamientoId = tipo,
            };
        }
    }

    /// <summary>
    /// Etapa 9a-2: EquipamientoSeeder con los 36 valores reales de equipamiento (en ejercicios SINTÉTICOS) y restricciones
    /// de la base. Usa una API propia para no alterar el catálogo de las otras pruebas.
    /// </summary>
    public class EquipamientoSeederTests : IClassFixture<EquipamientoSeederTests.ApiProduccion>
    {
        /// <summary>Production y sin datasets: como una base nueva en producción.</summary>
        public class ApiProduccion : GymBroApiFactory
        {
            protected override string Entorno => "Production";
        }

        private readonly ApiProduccion _api;
        public EquipamientoSeederTests(ApiProduccion api) { _api = api; }

        private static readonly string[] Valores =
        {
            "Assisted", "Assisted  Chest Dip", "Assisted (machine)", "Assisted (partner)", "Band-assisted", "Barbell", "Body Weight",
            "Cable", "Cable (pull side)", "Cable  Standing Fly", "Dumbbell", "Dynamic Stretch", "Lever", "Lever (plate loaded)",
            "Lever (selectorized)", "Lever (selectorized)​​​​​​​", "Lever (selectorized)  Chest Dip",
            "Plyometric", "Self-assisted", "Self-assisted​​​​​​​", "Sled", "Sled (plate loaded)",
            "Sled (selectorized)", "Smith", "Smith​​​​​​​", "Stretch", "StretchBroomstickDoorway",
            "Stretch  Fixed Bar Hugging", "Stretch (Gluteus Medius & Minimus)", "Stretch (Iliopsoas)", "Stretch (Piriformis)",
            "Stretch (Tensor Fasciae Latae)", "Suspended", "Suspension", "Weighted", "Weighted  Chest Dip",
        };

        [Fact]
        public async Task En_Production_sin_datasets_los_tipos_globales_existen_y_los_ocho_de_Core_tienen_tipo()
        {
            await _api.ConDbAsync(async db =>
            {
                Assert.Equal(13, await db.TiposEquipamiento.CountAsync(t => t.GimnasioId == null));
                var core = await db.Ejercicios.Where(e => e.Origen == OrigenEjercicio.Proyecto).Select(e => new { e.Nombre, e.TipoEquipamientoId }).ToListAsync();
                Assert.Equal(8, core.Count);
                Assert.Equal(CatalogoEquipamiento.IdDe(CatalogoEquipamiento.Polea), core.Single(c => c.Nombre == "Crunch en polea").TipoEquipamientoId);
                Assert.All(core.Where(c => c.Nombre != "Crunch en polea"),
                    c => Assert.Equal(CatalogoEquipamiento.IdDe(CatalogoEquipamiento.PesoCorporal), c.TipoEquipamientoId));
            });
        }

        [Fact]
        public async Task Los_36_valores_reales_quedan_con_tipo_sin_pisar_una_asignacion_y_es_idempotente()
        {
            var ids = Valores.ToDictionary(v => v, _ => Guid.NewGuid());
            var preasignado = ids["Barbell"];
            var tipoManual = CatalogoEquipamiento.IdDe(CatalogoEquipamiento.MaquinaOtra);
            await _api.ConDbAsync(db =>
            {
                db.Ejercicios.AddRange(Valores.Select(v => new Ejercicio
                {
                    Id = ids[v], Nombre = $"Sintetico {v}", Equipamiento = v, ZonaObjetivo = "Legs", Dificultad = 1,
                    TipoEquipamientoId = ids[v] == preasignado ? tipoManual : null,
                }));
                return Task.CompletedTask;
            });

            EquipamientoSeeder.Resultado primero = null!, segundo = null!;
            await _api.ConDbAsync(async db => primero = await EquipamientoSeeder.SeedAsync(db));
            await _api.ConDbAsync(async db => segundo = await EquipamientoSeeder.SeedAsync(db));

            Assert.Equal(35, primero.EjerciciosAsignados);
            Assert.Equal(0, primero.TiposInsertados);
            Assert.Equal((0, 0), (segundo.TiposInsertados, segundo.EjerciciosAsignados));

            await _api.ConDbAsync(async db =>
            {
                var filas = await db.Ejercicios.Where(e => ids.Values.Contains(e.Id)).Select(e => new { e.Id, e.Equipamiento, e.TipoEquipamientoId }).ToListAsync();
                Assert.Equal(36, filas.Count);
                Assert.All(filas.Where(f => f.Id != preasignado),
                    f => Assert.Equal(CatalogoEquipamiento.IdDe(CatalogoEquipamiento.Mapear(f.Equipamiento)), f.TipoEquipamientoId));
                Assert.Equal(tipoManual, filas.Single(f => f.Id == preasignado).TipoEquipamientoId);
            });
        }

        [Fact]
        public async Task Un_valor_desconocido_falla_de_forma_explicita_y_no_asigna_nada()
        {
            var desconocido = Guid.NewGuid();
            var conocido = Guid.NewGuid();
            await _api.ConDbAsync(db =>
            {
                db.Ejercicios.Add(new Ejercicio { Id = desconocido, Nombre = "Sintetico desconocido", Equipamiento = "Kettlebell", ZonaObjetivo = "Legs", Dificultad = 1 });
                db.Ejercicios.Add(new Ejercicio { Id = conocido, Nombre = "Sintetico conocido", Equipamiento = "Dumbbell", ZonaObjetivo = "Legs", Dificultad = 1 });
                return Task.CompletedTask;
            });
            try
            {
                var ex = await Assert.ThrowsAsync<EquipamientoDesconocidoException>(() => _api.ConDbAsync(db => EquipamientoSeeder.SeedAsync(db)));
                Assert.Equal("Kettlebell", ex.Valor);
                await _api.ConDbAsync(async db => Assert.Null((await db.Ejercicios.SingleAsync(e => e.Id == conocido)).TipoEquipamientoId));
            }
            finally
            {
                // Se retiran las dos filas para no afectar a las otras pruebas de esta clase.
                await _api.ConDbAsync(async db => db.Ejercicios.RemoveRange(
                    await db.Ejercicios.Where(e => e.Id == desconocido || e.Id == conocido).ToListAsync()));
            }
        }

        [Theory]
        [InlineData("Gimnasio", false)]   // origen Gimnasio sin gimnasio
        [InlineData("Dataset", true)]     // origen global con gimnasio
        public async Task El_CHECK_de_la_base_exige_coherencia_entre_origen_y_gimnasio(string origen, bool conGimnasio)
        {
            var g = new Gimnasio { Nombre = $"CHECK {origen}", Codigo = $"CK-{origen[..3].ToUpperInvariant()}" };
            await _api.ConDbAsync(db => { db.Gimnasios.Add(g); return Task.CompletedTask; });

            var ex = await Record.ExceptionAsync(() => _api.ConDbAsync(db =>
            {
                db.Ejercicios.Add(new Ejercicio
                {
                    Id = Guid.NewGuid(), Nombre = "Incoherente", ZonaObjetivo = "Legs", Dificultad = 1,
                    Origen = Enum.Parse<OrigenEjercicio>(origen), GimnasioId = conGimnasio ? g.Id : null,
                });
                return Task.CompletedTask;
            }));
            var dbEx = Assert.IsType<DbUpdateException>(ex);
            Assert.Contains("CK_Ejercicio_OrigenGimnasio", dbEx.InnerException!.Message);
        }

        [Fact]
        public async Task El_CHECK_de_cantidad_y_el_indice_unico_parcial_del_equipamiento()
        {
            var g = new Gimnasio { Nombre = "CHECK cantidad", Codigo = "CK-CANT" };
            await _api.ConDbAsync(db => { db.Gimnasios.Add(g); return Task.CompletedTask; });
            var tipo = CatalogoEquipamiento.IdDe(CatalogoEquipamiento.Barra);

            var cero = await Record.ExceptionAsync(() => _api.ConDbAsync(db =>
            {
                db.EquipamientosGimnasio.Add(new EquipamientoGimnasio { GimnasioId = g.Id, TipoEquipamientoId = tipo, Cantidad = 0 });
                return Task.CompletedTask;
            }));
            Assert.Contains("CK_EquipamientoGimnasio_Cantidad", Assert.IsType<DbUpdateException>(cero).InnerException!.Message);

            // Una fila dada de baja y una vigente del mismo tipo conviven; dos vigentes, no.
            await _api.ConDbAsync(db =>
            {
                db.EquipamientosGimnasio.Add(new EquipamientoGimnasio { GimnasioId = g.Id, TipoEquipamientoId = tipo, FechaBaja = DateTime.UtcNow });
                db.EquipamientosGimnasio.Add(new EquipamientoGimnasio { GimnasioId = g.Id, TipoEquipamientoId = tipo });
                return Task.CompletedTask;
            });
            var duplicado = await Record.ExceptionAsync(() => _api.ConDbAsync(db =>
            {
                db.EquipamientosGimnasio.Add(new EquipamientoGimnasio { GimnasioId = g.Id, TipoEquipamientoId = tipo });
                return Task.CompletedTask;
            }));
            Assert.IsType<DbUpdateException>(duplicado);
        }
    }
}
