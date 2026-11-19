using System.Net;
using System.Net.Http.Json;
using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Domain.Rules;
using GymBro.Infrastructure.Persistence.Seeders;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GymBro.Tests.Integration
{
    /// <summary>
    /// Etapa 9a-2b: administración del catálogo y del equipamiento por el Administrador de la Plataforma (D2), propuesta
    /// de ejercicios por el Personal Trainer (D4), desactivación en lugar de baja, aviso de equipamiento (O1) y estado del
    /// socio, contra la API real y PostgreSQL descartable.
    /// Escenario SINTÉTICO de prueba (sin datos reales):
    ///   G1: Dueño, dos Personal Trainers y socio A (con cuenta). G2: Dueño, Personal Trainer y socio B (con cuenta).
    ///   Administrador de la Plataforma sin gimnasio. Catálogo global: un ejercicio de Pecho con barra y Core (proyecto).
    /// </summary>
    public class AdministracionCatalogoTests : IClassFixture<AdministracionCatalogoTests.Escenario>
    {
        public sealed record Item(Guid Id, string Nombre, string Equipamiento, string Origen, Guid? GimnasioId, Guid? TipoEquipamientoId,
            bool Activo, Guid? CreadoPorUsuarioId, string? CreadoPor, bool PuedeEditar = false, bool PuedeCambiarEstado = false);
        public sealed record Pagina(List<Item> Items, int Total);
        public sealed record ItemEquipamiento(Guid TipoEquipamientoId, string Clave, string Nombre, bool SiempreDisponible, bool Propio, bool Marcado, int? Cantidad);
        public sealed record Equipamiento(Guid GimnasioId, string NombreGimnasio, bool Editable, List<ItemEquipamiento> Items);
        public sealed record Previsualizacion(int Cantidad, List<string> Ejercicios, List<string> TiposQuitados);
        public sealed record Auditoria(Guid? UsuarioId, string? Rol, Guid? GimnasioId, string Accion, string Entidad, string? EntidadId, string? Detalle);
        public sealed record SocioLista(Guid Id, string NombreCompleto, string Estado);
        public sealed record EjercicioRutina(Guid EjercicioId, string Ejercicio);
        public sealed record Rutina(Guid Id, List<EjercicioRutina> Ejercicios);

        /// <summary>Ejercicio del catálogo propio del proyecto (Core): "Plancha".</summary>
        private static readonly Guid CorePlancha = Guid.Parse("13e90ee5-9fb4-47bf-9abc-1277eae966d1");
        private static readonly Guid TipoPesoCorporal = CatalogoEquipamiento.IdDe(CatalogoEquipamiento.PesoCorporal);
        private static readonly Guid TipoMancuerna = CatalogoEquipamiento.IdDe(CatalogoEquipamiento.Mancuerna);
        private static readonly Guid TipoBanda = CatalogoEquipamiento.IdDe(CatalogoEquipamiento.Banda);

        private readonly Escenario _e;
        public AdministracionCatalogoTests(Escenario e) { _e = e; }

        private HttpClient Como(Usuario u) => _e.Api.ClienteComo(u);

        private static object Pedido(string nombre, Guid? gimnasioId = null, Guid? tipo = null, string zona = "Core",
            object? extra = null) => new
            {
                gimnasioId, nombre, zonaObjetivo = zona, tipoEquipamientoId = tipo ?? TipoPesoCorporal, musculoPrincipal = "Recto abdominal",
                ejecucion = "Sostener la posición indicada.", tipoMedida = "Repeticiones", dificultad = 2, creadoPorUsuarioId = extra
            };

        private async Task<Item> CrearAsync(Usuario quien, string nombre, Guid? gimnasioId = null, Guid? tipo = null)
        {
            var r = await Como(quien).PostAsJsonAsync("/api/Ejercicios", Pedido(nombre, gimnasioId, tipo));
            Assert.True(r.StatusCode == HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
            return (await r.Content.ReadFromJsonAsync<Item>())!;
        }

        private async Task<HttpResponseMessage> EstadoAsync(Usuario quien, Guid id, bool activo) =>
            await Como(quien).PutAsJsonAsync($"/api/Ejercicios/{id}/estado", new { activo });

        private async Task<List<Item>> ListarAsync(Usuario u, string query = "origen=Gimnasio") =>
            (await Como(u).GetFromJsonAsync<Pagina>($"/api/Ejercicios?tamanioPagina=100&{query}"))!.Items;

        private async Task<List<Auditoria>> AuditoriaAsync(string accion) =>
            (await Como(_e.SuperAdmin).GetFromJsonAsync<List<Auditoria>>($"/api/auditoria?cantidad=500&accion={accion}"))!;

        // ---- D2: el Administrador de la Plataforma sobre ejercicios propios de cualquier gimnasio ----

        [Fact]
        public async Task La_plataforma_crea_edita_desactiva_y_reactiva_ejercicios_propios_de_dos_gimnasios_y_queda_auditado()
        {
            var enG1 = await CrearAsync(_e.SuperAdmin, "Plataforma en G1", _e.G1.Id);
            var enG2 = await CrearAsync(_e.SuperAdmin, "Plataforma en G2", _e.G2.Id);
            Assert.Equal(("Gimnasio", _e.G1.Id, _e.SuperAdmin.Id), (enG1.Origen, enG1.GimnasioId, enG1.CreadoPorUsuarioId));
            Assert.Equal(_e.G2.Id, enG2.GimnasioId);

            // Cada gimnasio ve solo el suyo; el personal ve el rol del autor, no el nombre del Administrador.
            Assert.Contains(await ListarAsync(_e.Dueno1), i => i.Id == enG1.Id && i.CreadoPor == "Administrador de la Plataforma");
            Assert.DoesNotContain(await ListarAsync(_e.Dueno1), i => i.Id == enG2.Id);

            var editado = await Como(_e.SuperAdmin).PutAsJsonAsync($"/api/Ejercicios/{enG2.Id}", Pedido("Plataforma en G2 (v2)"));
            Assert.Equal(HttpStatusCode.OK, editado.StatusCode);
            Assert.Equal("Plataforma en G2 (v2)", (await editado.Content.ReadFromJsonAsync<Item>())!.Nombre);

            Assert.Equal(HttpStatusCode.OK, (await EstadoAsync(_e.SuperAdmin, enG2.Id, false)).StatusCode);
            Assert.DoesNotContain(await ListarAsync(_e.UsuarioSocioB), i => i.Id == enG2.Id);
            Assert.Contains(await ListarAsync(_e.SuperAdmin, "inactivos=true"), i => i.Id == enG2.Id);
            Assert.Equal(HttpStatusCode.OK, (await EstadoAsync(_e.SuperAdmin, enG2.Id, true)).StatusCode);
            Assert.Contains(await ListarAsync(_e.UsuarioSocioB), i => i.Id == enG2.Id);

            // Auditoría: rol de plataforma, gimnasio afectado y texto que lo indica.
            var altas = await AuditoriaAsync("ALTA_EJERCICIO");
            var altaG1 = Assert.Single(altas, a => a.EntidadId == enG1.Id.ToString());
            Assert.Equal((nameof(RolUsuario.SuperAdministrador), _e.G1.Id), (altaG1.Rol, altaG1.GimnasioId));
            Assert.Contains("Administrador de la Plataforma", altaG1.Detalle);
            Assert.Contains(await AuditoriaAsync("EDICION_EJERCICIO"), a => a.EntidadId == enG2.Id.ToString() && a.GimnasioId == _e.G2.Id);
            Assert.Contains(await AuditoriaAsync("DESACTIVAR_EJERCICIO"), a => a.EntidadId == enG2.Id.ToString() && a.GimnasioId == _e.G2.Id);
            Assert.Contains(await AuditoriaAsync("REACTIVAR_EJERCICIO"), a => a.EntidadId == enG2.Id.ToString() && a.GimnasioId == _e.G2.Id);
        }

        [Fact]
        public async Task La_plataforma_debe_indicar_un_gimnasio_existente()
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await Como(_e.SuperAdmin).PostAsJsonAsync("/api/Ejercicios", Pedido("Sin gimnasio"))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Como(_e.SuperAdmin).PostAsJsonAsync("/api/Ejercicios", Pedido("Gimnasio inexistente", Guid.NewGuid()))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Como(_e.SuperAdmin).PutAsJsonAsync("/api/equipamiento",
                new { gimnasioId = Guid.NewGuid(), items = Array.Empty<object>() })).StatusCode);
        }

        // ---- Aislamiento: el parámetro de gimnasio no sirve a los otros roles ----

        [Fact]
        public async Task Dueno_o_Personal_Trainer_que_indican_otro_gimnasio_reciben_404_y_no_se_escribe_nada()
        {
            Assert.Equal(HttpStatusCode.NotFound, (await Como(_e.Dueno1).PostAsJsonAsync("/api/Ejercicios", Pedido("Intruso dueno", _e.G2.Id))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Como(_e.Trainer1).PostAsJsonAsync("/api/Ejercicios", Pedido("Intruso trainer", _e.G2.Id))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Como(_e.Dueno1).PutAsJsonAsync("/api/equipamiento",
                new { gimnasioId = _e.G2.Id, items = new[] { new { tipoEquipamientoId = TipoMancuerna } } })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Como(_e.Dueno1).PostAsJsonAsync("/api/equipamiento/previsualizacion",
                new { gimnasioId = _e.G2.Id, items = Array.Empty<object>() })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Como(_e.Dueno1).PostAsJsonAsync("/api/equipamiento/tipos",
                new { gimnasioId = _e.G2.Id, nombre = "Tipo intruso" })).StatusCode);

            // Indicar el propio gimnasio es válido.
            var propio = await CrearAsync(_e.Dueno1, "Con gimnasio propio explícito", _e.G1.Id);
            Assert.Equal(_e.G1.Id, propio.GimnasioId);

            var intrusos = 0;
            await _e.Api.ConDbAsync(async db => intrusos = await db.Ejercicios.IgnoreQueryFilters().CountAsync(x => x.Nombre.StartsWith("Intruso")));
            Assert.Equal(0, intrusos);
        }

        [Fact]
        public async Task Personal_de_otro_gimnasio_no_ve_ni_modifica_los_ejercicios_ajenos()
        {
            var deG1 = await CrearAsync(_e.Trainer1, "Del trainer de G1");
            Assert.Equal(HttpStatusCode.NotFound, (await Como(_e.Trainer2).GetAsync($"/api/Ejercicios/{deG1.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Como(_e.Trainer2).PutAsJsonAsync($"/api/Ejercicios/{deG1.Id}", Pedido("Ajeno"))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await EstadoAsync(_e.Trainer2, deG1.Id, false)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await EstadoAsync(_e.Dueno2, deG1.Id, false)).StatusCode);
            Assert.DoesNotContain(await ListarAsync(_e.Trainer2), i => i.Id == deG1.Id);
            Assert.DoesNotContain(await ListarAsync(_e.Dueno2, "inactivos=true"), i => i.GimnasioId == _e.G1.Id);
        }

        // ---- D4: el Personal Trainer propone ejercicios ----

        [Fact]
        public async Task El_Personal_Trainer_crea_y_modifica_solo_los_suyos_y_el_Dueno_modifica_todos()
        {
            // El cuerpo intenta fijar el autor: se ignora, sale del JWT.
            var r = await Como(_e.Trainer1).PostAsJsonAsync("/api/Ejercicios", Pedido("Propuesto por trainer", extra: _e.Dueno1.Id));
            Assert.Equal(HttpStatusCode.Created, r.StatusCode);
            var suyo = (await r.Content.ReadFromJsonAsync<Item>())!;
            Assert.Equal((_e.G1.Id, _e.Trainer1.Id), (suyo.GimnasioId, suyo.CreadoPorUsuarioId));
            var autor = Guid.Empty;
            await _e.Api.ConDbAsync(async db => autor = (await db.Ejercicios.IgnoreQueryFilters().SingleAsync(x => x.Id == suyo.Id)).CreadoPorUsuarioId!.Value);
            Assert.Equal(_e.Trainer1.Id, autor);

            // "Propuesto por": el Dueño ve el nombre; el socio no.
            Assert.Contains(await ListarAsync(_e.Dueno1), i => i.Id == suyo.Id && i.CreadoPor == _e.Trainer1.NombreCompleto);
            Assert.Contains(await ListarAsync(_e.UsuarioSocioA), i => i.Id == suyo.Id && i.CreadoPor == null);

            Assert.Equal(HttpStatusCode.OK, (await Como(_e.Trainer1).PutAsJsonAsync($"/api/Ejercicios/{suyo.Id}", Pedido("Propuesto por trainer (v2)"))).StatusCode);

            // Lo que informa el catálogo coincide con lo que permite el servidor.
            Assert.Contains(await ListarAsync(_e.Trainer1), i => i.Id == suyo.Id && i.PuedeEditar && i.PuedeCambiarEstado);
            Assert.Contains(await ListarAsync(_e.Trainer1b), i => i.Id == suyo.Id && !i.PuedeEditar && !i.PuedeCambiarEstado);
            Assert.Contains(await ListarAsync(_e.Dueno1), i => i.Id == suyo.Id && i.PuedeEditar);
            Assert.Contains(await ListarAsync(_e.UsuarioSocioA), i => i.Id == suyo.Id && !i.PuedeEditar);

            // Otro Personal Trainer del mismo gimnasio: lo ve, pero no lo modifica (403).
            Assert.Equal(HttpStatusCode.OK, (await Como(_e.Trainer1b).GetAsync($"/api/Ejercicios/{suyo.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Como(_e.Trainer1b).PutAsJsonAsync($"/api/Ejercicios/{suyo.Id}", Pedido("Ajeno trainer"))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await EstadoAsync(_e.Trainer1b, suyo.Id, false)).StatusCode);

            // Uno del Dueño tampoco lo modifica el Personal Trainer.
            var delDueno = await CrearAsync(_e.Dueno1, "Del dueno para el trainer");
            Assert.Equal(HttpStatusCode.Forbidden, (await Como(_e.Trainer1).PutAsJsonAsync($"/api/Ejercicios/{delDueno.Id}", Pedido("Toma trainer"))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await EstadoAsync(_e.Trainer1, delDueno.Id, false)).StatusCode);

            // El Dueño edita y desactiva el del Personal Trainer; el Personal Trainer reactiva el suyo.
            Assert.Equal(HttpStatusCode.OK, (await Como(_e.Dueno1).PutAsJsonAsync($"/api/Ejercicios/{suyo.Id}", Pedido("Propuesto por trainer (v3)"))).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await EstadoAsync(_e.Dueno1, suyo.Id, false)).StatusCode);
            Assert.Contains(await ListarAsync(_e.Trainer1, "inactivos=true"), i => i.Id == suyo.Id);
            Assert.Equal(HttpStatusCode.OK, (await EstadoAsync(_e.Trainer1, suyo.Id, true)).StatusCode);
        }

        // ---- Desactivar en lugar de dar de baja ----

        [Fact]
        public async Task Desactivar_un_ejercicio_en_una_rutina_vigente_se_rechaza_tambien_para_la_plataforma()
        {
            var enRutina = await CrearAsync(_e.SuperAdmin, "En rutina vigente (plataforma)", _e.G1.Id);
            await _e.AgregarRutinaAsync(_e.SocioA.Id, enRutina.Id, EstadoPrescripcion.Editada);

            var r = await EstadoAsync(_e.SuperAdmin, enRutina.Id, false);
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
            Assert.Contains("rutina vigente", await r.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task La_plataforma_desactiva_y_reactiva_un_global_pero_no_lo_edita_y_el_baseline_no_usa_desactivados()
        {
            // La edición de un global se hace por script: la API responde 403 también a la plataforma.
            Assert.Equal(HttpStatusCode.Forbidden, (await Como(_e.SuperAdmin).PutAsJsonAsync($"/api/Ejercicios/{_e.PechoBarra}", Pedido("Global editado"))).StatusCode);
            Assert.Contains(await ListarAsync(_e.SuperAdmin, "origen=Dataset"), i => i.Id == _e.PechoBarra && !i.PuedeEditar && i.PuedeCambiarEstado);
            Assert.Contains(await ListarAsync(_e.Dueno2, "origen=Dataset"), i => i.Id == _e.PechoBarra && !i.PuedeEditar && !i.PuedeCambiarEstado);

            Assert.Equal(HttpStatusCode.OK, (await EstadoAsync(_e.SuperAdmin, _e.PechoBarra, false)).StatusCode);
            try
            {
                // Nadie más lo ve: ni el Dueño (tampoco en inactivos, porque no es propio) ni el socio.
                Assert.Equal(HttpStatusCode.NotFound, (await Como(_e.Dueno2).GetAsync($"/api/Ejercicios/{_e.PechoBarra}")).StatusCode);
                Assert.DoesNotContain(await ListarAsync(_e.Dueno2, "inactivos=true&origen="), i => i.Id == _e.PechoBarra);
                Assert.Contains(await ListarAsync(_e.SuperAdmin, "inactivos=true&origen=Dataset"), i => i.Id == _e.PechoBarra && !i.Activo);

                // El baseline no lo prescribe (G2 no declaró equipamiento: sin desactivar, sería el elegido de Pecho).
                var rutina = await _e.GenerarBaselineAsync(_e.UsuarioSocioB, _e.SocioB.Id);
                Assert.DoesNotContain(rutina.Ejercicios, x => x.EjercicioId == _e.PechoBarra);
            }
            finally
            {
                Assert.Equal(HttpStatusCode.OK, (await EstadoAsync(_e.SuperAdmin, _e.PechoBarra, true)).StatusCode);
            }
            Assert.Equal(HttpStatusCode.OK, (await Como(_e.Dueno2).GetAsync($"/api/Ejercicios/{_e.PechoBarra}")).StatusCode);
        }

        [Fact]
        public async Task No_se_puede_crear_un_ejercicio_con_el_nombre_de_uno_desactivado()
        {
            var original = await CrearAsync(_e.Dueno2, "Nombre reservado G2");
            Assert.Equal(HttpStatusCode.OK, (await EstadoAsync(_e.Dueno2, original.Id, false)).StatusCode);
            var r = await Como(_e.Dueno2).PostAsJsonAsync("/api/Ejercicios", Pedido("NOMBRE RESERVADO G2"));
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        }

        // ---- Seeders frente a lo que hace la plataforma (equivalente a dos arranques) ----

        [Fact]
        public async Task Los_seeders_no_reactivan_ni_pisan_lo_que_cambio_la_plataforma()
        {
            // Un ejercicio de Core desactivado y un tipo global renombrado.
            Assert.Equal(HttpStatusCode.OK, (await EstadoAsync(_e.SuperAdmin, CorePlancha, false)).StatusCode);
            var renombre = await Como(_e.SuperAdmin).PutAsJsonAsync($"/api/equipamiento/tipos/{TipoBanda}", new { nombre = "Bandas de resistencia" });
            Assert.Equal(HttpStatusCode.OK, renombre.StatusCode);
            try
            {
                // Segundo arranque: los mismos seeders que Program.cs, en el mismo orden.
                await _e.Api.ConDbAsync(async db => await CatalogoProyectoSeeder.SeedAsync(db));
                await _e.Api.ConDbAsync(async db => await EquipamientoSeeder.SeedAsync(db));

                await _e.Api.ConDbAsync(async db =>
                {
                    var plancha = await db.Ejercicios.IgnoreQueryFilters().SingleAsync(x => x.Id == CorePlancha);
                    Assert.False(plancha.Activo);
                    var tipo = await db.TiposEquipamiento.IgnoreQueryFilters().SingleAsync(t => t.Id == TipoBanda);
                    Assert.Equal("Bandas de resistencia", tipo.Nombre);
                    Assert.Equal(CatalogoEquipamiento.Banda, tipo.Clave);
                });
            }
            finally
            {
                await EstadoAsync(_e.SuperAdmin, CorePlancha, true);
                await Como(_e.SuperAdmin).PutAsJsonAsync($"/api/equipamiento/tipos/{TipoBanda}", new { nombre = "Banda elástica" });
            }
        }

        // ---- Tipos y marcado de equipamiento ----

        [Fact]
        public async Task La_plataforma_gestiona_tipos_y_marcado_de_cualquier_gimnasio_y_el_Dueno_los_propios()
        {
            // Alta de un tipo propio en G2 por la plataforma, marcado y edición.
            var alta = await Como(_e.SuperAdmin).PostAsJsonAsync("/api/equipamiento/tipos", new { gimnasioId = _e.G2.Id, nombre = "Trineo G2" });
            Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
            var tipo = (await alta.Content.ReadFromJsonAsync<ItemEquipamiento>())!;

            var marcar = await Como(_e.SuperAdmin).PutAsJsonAsync("/api/equipamiento",
                new { gimnasioId = _e.G2.Id, items = new[] { new { tipoEquipamientoId = tipo.TipoEquipamientoId } } });
            Assert.Equal(HttpStatusCode.OK, marcar.StatusCode);
            var g2 = (await Como(_e.Dueno2).GetFromJsonAsync<Equipamiento>("/api/equipamiento"))!;
            Assert.True(g2.Items.Single(i => i.TipoEquipamientoId == tipo.TipoEquipamientoId).Marcado);
            Assert.Contains(await AuditoriaAsync("CAMBIO_EQUIPAMIENTO"), a => a.Rol == nameof(RolUsuario.SuperAdministrador) && a.GimnasioId == _e.G2.Id);

            Assert.Equal(HttpStatusCode.OK, (await Como(_e.SuperAdmin).PutAsJsonAsync($"/api/equipamiento/tipos/{tipo.TipoEquipamientoId}", new { nombre = "Trineo de G2" })).StatusCode);

            // Un ejercicio propio lo usa: la baja se rechaza (también si estuviera desactivado).
            var conTipo = await CrearAsync(_e.Dueno2, "Empuje de trineo", tipo: tipo.TipoEquipamientoId);
            Assert.Equal("Trineo de G2", conTipo.Equipamiento);
            Assert.Equal(HttpStatusCode.OK, (await EstadoAsync(_e.Dueno2, conTipo.Id, false)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Como(_e.SuperAdmin).DeleteAsync($"/api/equipamiento/tipos/{tipo.TipoEquipamientoId}")).StatusCode);

            // El Dueño renombra su tipo: el texto de equipamiento de sus ejercicios acompaña.
            Assert.Equal(HttpStatusCode.OK, (await Como(_e.Dueno2).PutAsJsonAsync($"/api/equipamiento/tipos/{tipo.TipoEquipamientoId}", new { nombre = "Trineo" })).StatusCode);
            var texto = "";
            await _e.Api.ConDbAsync(async db => texto = (await db.Ejercicios.IgnoreQueryFilters().SingleAsync(x => x.Id == conTipo.Id)).Equipamiento);
            Assert.Equal("Trineo", texto);

            // Nombre repetido de un global: 400. Global: el Dueño no lo toca (403); otro Dueño no ve el propio (404).
            Assert.Equal(HttpStatusCode.BadRequest, (await Como(_e.Dueno2).PutAsJsonAsync($"/api/equipamiento/tipos/{tipo.TipoEquipamientoId}", new { nombre = "BARRA" })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Como(_e.Dueno2).PutAsJsonAsync($"/api/equipamiento/tipos/{TipoMancuerna}", new { nombre = "Mancuernas" })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Como(_e.Dueno1).DeleteAsync($"/api/equipamiento/tipos/{tipo.TipoEquipamientoId}")).StatusCode);

            // Un tipo sin uso se da de baja y deja de estar marcado.
            var libre = (await (await Como(_e.Dueno2).PostAsJsonAsync("/api/equipamiento/tipos", new { nombre = "Soga G2" })).Content.ReadFromJsonAsync<ItemEquipamiento>())!;
            await Como(_e.Dueno2).PutAsJsonAsync("/api/equipamiento", new { items = new[] { new { tipoEquipamientoId = libre.TipoEquipamientoId } } });
            Assert.Equal(HttpStatusCode.NoContent, (await Como(_e.Dueno2).DeleteAsync($"/api/equipamiento/tipos/{libre.TipoEquipamientoId}")).StatusCode);
            var final = (await Como(_e.Dueno2).GetFromJsonAsync<Equipamiento>("/api/equipamiento"))!;
            Assert.DoesNotContain(final.Items, i => i.TipoEquipamientoId == libre.TipoEquipamientoId);
            var marcadosVigentes = -1;
            await _e.Api.ConDbAsync(async db => marcadosVigentes = await db.EquipamientosGimnasio.IgnoreQueryFilters()
                .CountAsync(x => x.TipoEquipamientoId == libre.TipoEquipamientoId && x.FechaBaja == null));
            Assert.Equal(0, marcadosVigentes);
            Assert.Contains(await AuditoriaAsync("BAJA_TIPO_EQUIPAMIENTO"), a => a.EntidadId == libre.TipoEquipamientoId.ToString() && a.GimnasioId == _e.G2.Id);
        }

        // ---- O1: previsualización al quitar equipamiento ----

        [Fact]
        public async Task La_previsualizacion_informa_los_ejercicios_afectados_sin_modificar_datos()
        {
            // G1 marca mancuernas y tiene un ejercicio propio que las usa.
            Assert.Equal(HttpStatusCode.OK, (await Como(_e.Dueno1).PutAsJsonAsync("/api/equipamiento",
                new { items = new[] { new { tipoEquipamientoId = TipoMancuerna, cantidad = (int?)4 } } })).StatusCode);
            var conMancuerna = await CrearAsync(_e.Dueno1, "Curl propio con mancuerna", tipo: TipoMancuerna);

            int MarcadosG1(GymBro.Infrastructure.Persistence.GymBroDbContext db) => db.EquipamientosGimnasio.IgnoreQueryFilters().Count(x => x.GimnasioId == _e.G1.Id);
            int filasAntes = 0, filasDespues = 0, auditAntes = 0, auditDespues = 0;
            await _e.Api.ConDbAsync(db => { filasAntes = MarcadosG1(db); auditAntes = db.AuditLogs.Count(); return Task.CompletedTask; });

            var r = await Como(_e.Dueno1).PostAsJsonAsync("/api/equipamiento/previsualizacion", new { items = Array.Empty<object>() });
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            var p = (await r.Content.ReadFromJsonAsync<Previsualizacion>())!;
            Assert.True(p.Cantidad >= 1);
            Assert.Contains(conMancuerna.Nombre, p.Ejercicios);
            Assert.Contains("Mancuernas", p.TiposQuitados);
            Assert.True(p.Ejercicios.Count <= 10);

            // Mantener las mancuernas no afecta a ninguno; la plataforma puede previsualizar indicando el gimnasio.
            var sinCambios = (await (await Como(_e.SuperAdmin).PostAsJsonAsync("/api/equipamiento/previsualizacion",
                new { gimnasioId = _e.G1.Id, items = new[] { new { tipoEquipamientoId = TipoMancuerna } } })).Content.ReadFromJsonAsync<Previsualizacion>())!;
            Assert.Equal(0, sinCambios.Cantidad);

            await _e.Api.ConDbAsync(db => { filasDespues = MarcadosG1(db); auditDespues = db.AuditLogs.Count(); return Task.CompletedTask; });
            Assert.Equal((filasAntes, auditAntes), (filasDespues, auditDespues));
            var g1 = (await Como(_e.Dueno1).GetFromJsonAsync<Equipamiento>("/api/equipamiento"))!;
            Assert.True(g1.Items.Single(i => i.TipoEquipamientoId == TipoMancuerna).Marcado);
        }

        // ---- Estado administrativo del socio ----

        [Fact]
        public async Task El_Dueno_cambia_el_estado_del_socio_de_forma_informativa()
        {
            var r = await Como(_e.Dueno1).PutAsJsonAsync($"/api/Socio/{_e.SocioA.Id}/estado", new { estado = "inactivo" });
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            try
            {
                var lista = await Como(_e.Dueno1).GetFromJsonAsync<List<SocioLista>>("/api/Socio");
                Assert.Equal("Inactivo", lista!.Single(s => s.Id == _e.SocioA.Id).Estado);
                // Informativo: el socio sigue entrando y consultando su catálogo.
                Assert.Equal(HttpStatusCode.OK, (await Como(_e.UsuarioSocioA).GetAsync("/api/Ejercicios?tamanioPagina=1")).StatusCode);
                Assert.Contains(await AuditoriaAsync("CAMBIO_ESTADO_SOCIO"), a => a.EntidadId == _e.SocioA.Id.ToString() && a.Detalle == "Inactivo");

                Assert.Equal(HttpStatusCode.BadRequest, (await Como(_e.Dueno1).PutAsJsonAsync($"/api/Socio/{_e.SocioA.Id}/estado", new { estado = "Suspendido" })).StatusCode);
                Assert.Equal(HttpStatusCode.BadRequest, (await Como(_e.Dueno1).PutAsJsonAsync($"/api/Socio/{_e.SocioA.Id}/estado", new { estado = "2" })).StatusCode);
                Assert.Equal(HttpStatusCode.NotFound, (await Como(_e.Dueno2).PutAsJsonAsync($"/api/Socio/{_e.SocioA.Id}/estado", new { estado = "Activo" })).StatusCode);
            }
            finally
            {
                await Como(_e.Dueno1).PutAsJsonAsync($"/api/Socio/{_e.SocioA.Id}/estado", new { estado = "Activo" });
            }
        }

        // ---- Escenario ----

        public class Escenario : IAsyncLifetime
        {
            public ApiConIaFalsa Api { get; } = new();
            public Gimnasio G1 { get; } = new() { Nombre = "Gimnasio 1 (9a-2b)", Codigo = "AD-1", RequiereAprobacionRutinas = false };
            public Gimnasio G2 { get; } = new() { Nombre = "Gimnasio 2 (9a-2b)", Codigo = "AD-2", RequiereAprobacionRutinas = false };

            public Usuario Dueno1 { get; private set; } = null!;
            public Usuario Dueno2 { get; private set; } = null!;
            public Usuario Trainer1 { get; private set; } = null!;
            public Usuario Trainer1b { get; private set; } = null!;
            public Usuario Trainer2 { get; private set; } = null!;
            public Usuario SuperAdmin { get; private set; } = null!;
            public Socio SocioA { get; private set; } = null!;
            public Socio SocioB { get; private set; } = null!;
            public Usuario UsuarioSocioA { get; private set; } = null!;
            public Usuario UsuarioSocioB { get; private set; } = null!;
            public Guid PechoBarra { get; } = Guid.NewGuid();

            public async Task InitializeAsync()
            {
                await Api.InitializeAsync();
                Dueno1 = Personal("dueno1@ad.test", "Dueño Uno", RolUsuario.AdministradorGimnasio, G1.Id);
                Dueno2 = Personal("dueno2@ad.test", "Dueño Dos", RolUsuario.AdministradorGimnasio, G2.Id);
                Trainer1 = Personal("trainer1@ad.test", "Trainer Uno", RolUsuario.Entrenador, G1.Id);
                Trainer1b = Personal("trainer1b@ad.test", "Trainer Uno B", RolUsuario.Entrenador, G1.Id);
                Trainer2 = Personal("trainer2@ad.test", "Trainer Dos", RolUsuario.Entrenador, G2.Id);
                SuperAdmin = new Usuario { Email = "plataforma@ad.test", NombreCompleto = "Plataforma Prueba", Rol = RolUsuario.SuperAdministrador, PasswordHash = "-" };
                (SocioA, UsuarioSocioA) = NuevoSocio("a@ad.test", G1.Id);
                (SocioB, UsuarioSocioB) = NuevoSocio("b@ad.test", G2.Id);

                await Api.ConDbAsync(db =>
                {
                    db.Gimnasios.AddRange(G1, G2);
                    db.Socios.AddRange(SocioA, SocioB);
                    db.Usuarios.AddRange(Dueno1, Dueno2, Trainer1, Trainer1b, Trainer2, SuperAdmin, UsuarioSocioA, UsuarioSocioB);
                    db.MedicionesFisicas.AddRange(new[] { SocioA, SocioB }.Select(s => new MedicionFisica { SocioId = s.Id, PesoKg = 70, AlturaCm = 175, IMC = 22.9m }));
                    db.Ejercicios.Add(new Ejercicio
                    {
                        Id = PechoBarra, Nombre = "AAA Pecho con barra (9a-2b)", Equipamiento = "Barbell", ZonaObjetivo = "Chest", MusculoPrincipal = "Pectoral",
                        Ejecucion = "-", Dificultad = 1, TipoEquipamientoId = CatalogoEquipamiento.IdDe(CatalogoEquipamiento.Barra),
                    });
                    db.Dosificaciones.Add(new DosificacionVolumetrica
                    {
                        Id = Guid.NewGuid(), ParteCuerpo = "Chest", SubzonaObjetivo = "General", ZonaObjetivo = "Chest",
                        SeriesMinimas = 3, SeriesMaximas = 4, RepeticionesMinimas = 8, RepeticionesMaximas = 12,
                    });
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

            private static Usuario Personal(string email, string nombre, RolUsuario rol, Guid gimnasioId) =>
                new() { Email = email, NombreCompleto = nombre, Rol = rol, GimnasioId = gimnasioId, PasswordHash = "-" };

            private static (Socio, Usuario) NuevoSocio(string email, Guid gimnasioId)
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
        }
    }
}
