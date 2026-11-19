using System.Net;
using System.Net.Http.Json;
using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GymBro.Tests.Integration
{
    /// <summary>
    /// Etapa 9a-2c (D1): el Administrador de la Plataforma lista y resuelve vinculaciones y lee y cambia la política de
    /// revisión de cualquier gimnasio, indicando gimnasioId; el Dueño sigue limitado al suyo. Todo queda auditado con el
    /// gimnasio afectado. Contra la API real y PostgreSQL descartable.
    /// Escenario SINTÉTICO de prueba (sin datos reales):
    ///   G1 y G2 con su Dueño; Administrador de la Plataforma sin gimnasio; socios sin gimnasio con solicitudes pendientes.
    /// </summary>
    public class VinculacionesPlataformaTests : IClassFixture<VinculacionesPlataformaTests.Escenario>
    {
        public sealed record Solicitud(Guid Id, Guid SocioId, string NombreSocio, string Dni, Guid GimnasioId, string Estado, string? Motivo);
        public sealed record GimnasioDto(Guid Id, string Nombre, string Codigo, bool RequiereAprobacionRutinas, bool Activo);
        public sealed record Auditoria(Guid? UsuarioId, string? Rol, Guid? GimnasioId, string Accion, string Entidad, string? EntidadId, string? Detalle);

        private readonly Escenario _e;
        public VinculacionesPlataformaTests(Escenario e) { _e = e; }

        private HttpClient Como(Usuario u) => _e.Api.ClienteComo(u);

        private async Task<List<Solicitud>> PendientesAsync(Usuario quien, Guid? gimnasioId = null) =>
            (await Como(quien).GetFromJsonAsync<List<Solicitud>>(
                "/api/vinculaciones/pendientes" + (gimnasioId is null ? "" : $"?gimnasioId={gimnasioId}")))!;

        private async Task<List<Auditoria>> AuditoriaAsync(string accion) =>
            (await Como(_e.SuperAdmin).GetFromJsonAsync<List<Auditoria>>($"/api/auditoria?cantidad=500&accion={accion}"))!;

        // ---- Vinculaciones ----

        [Fact]
        public async Task La_plataforma_lista_las_pendientes_de_cualquier_gimnasio_indicando_gimnasioId()
        {
            var s1 = await _e.SolicitudAsync("40000001", _e.G1.Id);
            var s2 = await _e.SolicitudAsync("40000002", _e.G2.Id);

            var enG1 = await PendientesAsync(_e.SuperAdmin, _e.G1.Id);
            var enG2 = await PendientesAsync(_e.SuperAdmin, _e.G2.Id);

            Assert.Contains(enG1, s => s.Id == s1.Id);
            Assert.DoesNotContain(enG1, s => s.Id == s2.Id);
            Assert.Contains(enG2, s => s.Id == s2.Id);
            Assert.All(enG1, s => Assert.Equal(_e.G1.Id, s.GimnasioId));
        }

        [Fact]
        public async Task La_plataforma_aprueba_y_rechaza_en_dos_gimnasios_y_queda_auditado_con_el_gimnasio()
        {
            var aprobar = await _e.SolicitudAsync("40000003", _e.G1.Id);
            var rechazar = await _e.SolicitudAsync("40000004", _e.G2.Id);

            var r1 = await Como(_e.SuperAdmin).PostAsJsonAsync($"/api/vinculaciones/{aprobar.Id}/resolver",
                new { aprobar = true, gimnasioId = _e.G1.Id });
            Assert.True(r1.StatusCode == HttpStatusCode.OK, await r1.Content.ReadAsStringAsync());
            Assert.Equal("Aprobada", (await r1.Content.ReadFromJsonAsync<Solicitud>())!.Estado);

            var r2 = await Como(_e.SuperAdmin).PostAsJsonAsync($"/api/vinculaciones/{rechazar.Id}/resolver",
                new { aprobar = false, motivo = "Datos sin verificar (prueba)", gimnasioId = _e.G2.Id });
            Assert.True(r2.StatusCode == HttpStatusCode.OK, await r2.Content.ReadAsStringAsync());
            Assert.Equal("Rechazada", (await r2.Content.ReadFromJsonAsync<Solicitud>())!.Estado);

            await _e.Api.ConDbAsync(async db =>
            {
                var socio = await db.Socios.IgnoreQueryFilters().SingleAsync(s => s.Id == aprobar.SocioId);
                Assert.Equal(_e.G1.Id, socio.GimnasioId);
                var noVinculado = await db.Socios.IgnoreQueryFilters().SingleAsync(s => s.Id == rechazar.SocioId);
                Assert.Null(noVinculado.GimnasioId);
                var resuelta = await db.SolicitudesVinculacion.IgnoreQueryFilters().SingleAsync(s => s.Id == aprobar.Id);
                Assert.Equal(_e.SuperAdmin.Id, resuelta.ResueltaPorUsuarioId);
            });

            var aprobadas = await AuditoriaAsync("VINCULACION_APROBADA");
            var a = Assert.Single(aprobadas, x => x.EntidadId == aprobar.Id.ToString());
            Assert.Equal((nameof(RolUsuario.SuperAdministrador), _e.G1.Id, _e.SuperAdmin.Id), (a.Rol, a.GimnasioId, a.UsuarioId));
            Assert.Contains("Administrador de la Plataforma", a.Detalle);

            var rechazadas = await AuditoriaAsync("VINCULACION_RECHAZADA");
            var b = Assert.Single(rechazadas, x => x.EntidadId == rechazar.Id.ToString());
            Assert.Equal(_e.G2.Id, b.GimnasioId);
        }

        [Fact]
        public async Task La_plataforma_no_resuelve_una_solicitud_indicando_otro_gimnasio()
        {
            var s = await _e.SolicitudAsync("40000005", _e.G1.Id);

            var r = await Como(_e.SuperAdmin).PostAsJsonAsync($"/api/vinculaciones/{s.Id}/resolver",
                new { aprobar = true, gimnasioId = _e.G2.Id });

            Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
            Assert.Contains(await PendientesAsync(_e.SuperAdmin, _e.G1.Id), x => x.Id == s.Id);
        }

        [Fact]
        public async Task Sin_gimnasioId_la_plataforma_recibe_400_con_mensaje_claro_y_no_se_escribe_nada()
        {
            var s = await _e.SolicitudAsync("40000006", _e.G1.Id);
            var antes = (await _e.GimnasioAsync(_e.G1.Id)).RequiereAprobacionRutinas;

            var listar = await Como(_e.SuperAdmin).GetAsync("/api/vinculaciones/pendientes");
            var resolver = await Como(_e.SuperAdmin).PostAsJsonAsync($"/api/vinculaciones/{s.Id}/resolver", new { aprobar = true });
            var leer = await Como(_e.SuperAdmin).GetAsync("/api/mi-gimnasio");
            var politica = await Como(_e.SuperAdmin).PutAsJsonAsync("/api/mi-gimnasio/politica-revision",
                new { requiereAprobacionRutinas = !antes });

            foreach (var r in new[] { listar, resolver, leer, politica })
            {
                Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
                Assert.Contains("Indique el gimnasio", await r.Content.ReadAsStringAsync());
            }
            Assert.Contains(await PendientesAsync(_e.SuperAdmin, _e.G1.Id), x => x.Id == s.Id);
            Assert.Equal(antes, (await _e.GimnasioAsync(_e.G1.Id)).RequiereAprobacionRutinas);
        }

        [Fact]
        public async Task La_plataforma_con_un_gimnasio_inexistente_recibe_404()
        {
            Assert.Equal(HttpStatusCode.NotFound, (await Como(_e.SuperAdmin).GetAsync($"/api/vinculaciones/pendientes?gimnasioId={Guid.NewGuid()}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Como(_e.SuperAdmin).GetAsync($"/api/mi-gimnasio?gimnasioId={Guid.NewGuid()}")).StatusCode);
        }

        [Fact]
        public async Task El_Dueno_sigue_limitado_a_su_gimnasio_y_un_gimnasioId_ajeno_responde_404()
        {
            var propia = await _e.SolicitudAsync("40000007", _e.G1.Id);
            var ajena = await _e.SolicitudAsync("40000008", _e.G2.Id);

            // Sin gimnasioId: igual que antes de la etapa.
            var suyas = await PendientesAsync(_e.Dueno1);
            Assert.Contains(suyas, x => x.Id == propia.Id);
            Assert.DoesNotContain(suyas, x => x.Id == ajena.Id);
            Assert.Equal(_e.G1.Id, (await Como(_e.Dueno1).GetFromJsonAsync<GimnasioDto>("/api/mi-gimnasio"))!.Id);

            // Con gimnasioId ajeno: 404 en lectura y escritura, sin efectos.
            Assert.Equal(HttpStatusCode.NotFound, (await Como(_e.Dueno1).GetAsync($"/api/vinculaciones/pendientes?gimnasioId={_e.G2.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Como(_e.Dueno1).GetAsync($"/api/mi-gimnasio?gimnasioId={_e.G2.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Como(_e.Dueno1).PostAsJsonAsync($"/api/vinculaciones/{ajena.Id}/resolver",
                new { aprobar = true, gimnasioId = _e.G2.Id })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Como(_e.Dueno1).PostAsJsonAsync($"/api/vinculaciones/{ajena.Id}/resolver",
                new { aprobar = true })).StatusCode);
            var politicaG2 = (await _e.GimnasioAsync(_e.G2.Id)).RequiereAprobacionRutinas;
            Assert.Equal(HttpStatusCode.NotFound, (await Como(_e.Dueno1).PutAsJsonAsync("/api/mi-gimnasio/politica-revision",
                new { requiereAprobacionRutinas = !politicaG2, gimnasioId = _e.G2.Id })).StatusCode);
            Assert.Equal(politicaG2, (await _e.GimnasioAsync(_e.G2.Id)).RequiereAprobacionRutinas);
            Assert.Contains(await PendientesAsync(_e.SuperAdmin, _e.G2.Id), x => x.Id == ajena.Id);

            // Su propio gimnasio indicado explícitamente: permitido.
            var r = await Como(_e.Dueno1).PostAsJsonAsync($"/api/vinculaciones/{propia.Id}/resolver", new { aprobar = false, gimnasioId = _e.G1.Id });
            Assert.True(r.StatusCode == HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
            var registro = Assert.Single(await AuditoriaAsync("VINCULACION_RECHAZADA"), x => x.EntidadId == propia.Id.ToString());
            Assert.Equal((nameof(RolUsuario.AdministradorGimnasio), _e.G1.Id), (registro.Rol, registro.GimnasioId));
        }

        // ---- Política de revisión ----

        [Fact]
        public async Task La_plataforma_lee_y_cambia_la_politica_de_revision_de_cualquier_gimnasio_y_queda_auditado()
        {
            var g2 = await Como(_e.SuperAdmin).GetFromJsonAsync<GimnasioDto>($"/api/mi-gimnasio?gimnasioId={_e.G2.Id}");
            Assert.Equal((_e.G2.Id, _e.G2.Nombre), (g2!.Id, g2.Nombre));

            var nuevo = !g2.RequiereAprobacionRutinas;
            var r = await Como(_e.SuperAdmin).PutAsJsonAsync("/api/mi-gimnasio/politica-revision",
                new { requiereAprobacionRutinas = nuevo, gimnasioId = _e.G2.Id });
            Assert.True(r.StatusCode == HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
            Assert.Equal(nuevo, (await r.Content.ReadFromJsonAsync<GimnasioDto>())!.RequiereAprobacionRutinas);
            Assert.Equal(nuevo, (await _e.GimnasioAsync(_e.G2.Id)).RequiereAprobacionRutinas);

            // El Dueño de G2 ve el cambio; G1 no se tocó.
            Assert.Equal(nuevo, (await Como(_e.Dueno2).GetFromJsonAsync<GimnasioDto>("/api/mi-gimnasio"))!.RequiereAprobacionRutinas);

            var registro = Assert.Single(await AuditoriaAsync("POLITICA_REVISION"),
                x => x.GimnasioId == _e.G2.Id && x.Rol == nameof(RolUsuario.SuperAdministrador));
            Assert.Equal((_e.SuperAdmin.Id, _e.G2.Id.ToString()), (registro.UsuarioId, registro.EntidadId));
            Assert.Contains($"RequiereAprobacion={nuevo}", registro.Detalle);
            Assert.Contains("Administrador de la Plataforma", registro.Detalle);
        }

        [Fact]
        public async Task El_Dueno_cambia_la_politica_de_su_gimnasio_como_antes()
        {
            var actual = (await _e.GimnasioAsync(_e.G1.Id)).RequiereAprobacionRutinas;
            var r = await Como(_e.Dueno1).PutAsJsonAsync("/api/mi-gimnasio/politica-revision", new { requiereAprobacionRutinas = !actual });
            Assert.True(r.StatusCode == HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
            Assert.Equal(!actual, (await _e.GimnasioAsync(_e.G1.Id)).RequiereAprobacionRutinas);
            Assert.Contains(await AuditoriaAsync("POLITICA_REVISION"),
                x => x.GimnasioId == _e.G1.Id && x.Rol == nameof(RolUsuario.AdministradorGimnasio));
        }

        // ---- Escenario ----

        public class Escenario : IAsyncLifetime
        {
            public GymBroApiFactory Api { get; } = new();
            public Gimnasio G1 { get; } = new() { Nombre = "Gimnasio 1 (9a-2c)", Codigo = "VP-1", RequiereAprobacionRutinas = true };
            public Gimnasio G2 { get; } = new() { Nombre = "Gimnasio 2 (9a-2c)", Codigo = "VP-2", RequiereAprobacionRutinas = false };
            public Usuario Dueno1 { get; private set; } = null!;
            public Usuario Dueno2 { get; private set; } = null!;
            public Usuario SuperAdmin { get; private set; } = null!;

            public async Task InitializeAsync()
            {
                await Api.InitializeAsync();
                Dueno1 = new Usuario { Email = "dueno1@vp.test", NombreCompleto = "Dueño Uno", Rol = RolUsuario.AdministradorGimnasio, GimnasioId = G1.Id, PasswordHash = "-" };
                Dueno2 = new Usuario { Email = "dueno2@vp.test", NombreCompleto = "Dueño Dos", Rol = RolUsuario.AdministradorGimnasio, GimnasioId = G2.Id, PasswordHash = "-" };
                SuperAdmin = new Usuario { Email = "plataforma@vp.test", NombreCompleto = "Plataforma Prueba", Rol = RolUsuario.SuperAdministrador, PasswordHash = "-" };
                await Api.ConDbAsync(db =>
                {
                    db.Gimnasios.AddRange(G1, G2);
                    db.Usuarios.AddRange(Dueno1, Dueno2, SuperAdmin);
                    return Task.CompletedTask;
                });
            }

            /// <summary>Socio sin gimnasio con una solicitud pendiente en el gimnasio indicado (DNI sintético).</summary>
            public async Task<SolicitudVinculacion> SolicitudAsync(string dni, Guid gimnasioId)
            {
                var socio = new Socio
                {
                    Id = Guid.NewGuid(), Nombre = "Socio " + dni, Apellido = "Prueba", Email = $"s{dni}@vp.test", Dni = dni, Genero = "F",
                    FechaNacimiento = new DateTime(1995, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    Objetivo = ObjetivoEntrenamiento.Mantenimiento, Nivel = NivelExperiencia.Principiante,
                };
                var solicitud = new SolicitudVinculacion { SocioId = socio.Id, GimnasioId = gimnasioId, NombreSocio = socio.NombreCompleto, DniDeclarado = dni };
                await Api.ConDbAsync(db =>
                {
                    db.Socios.Add(socio);
                    db.SolicitudesVinculacion.Add(solicitud);
                    return Task.CompletedTask;
                });
                return solicitud;
            }

            public async Task<Gimnasio> GimnasioAsync(Guid id)
            {
                Gimnasio? g = null;
                await Api.ConDbAsync(async db => g = await db.Gimnasios.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == id));
                return g!;
            }

            public async Task DisposeAsync() => await Api.DisposeAsync();
        }
    }
}
