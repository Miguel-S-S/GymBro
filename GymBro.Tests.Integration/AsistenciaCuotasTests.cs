using System.Net;
using System.Net.Http.Json;
using GymBro.Application.Services;
using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace GymBro.Tests.Integration
{
    /// <summary>
    /// Etapa 11: asistencia (QR y manual) y cuotas contra la API real (Postgres descartable). Escenario SINTETICO:
    ///   Gimnasio 1: Dueno 1, Personal Trainer 1, socios A (con cuenta) y B (activos) e I (inactivo).
    ///   Gimnasio 2: Dueno 2 y socio C (con cuenta). Socio X independiente (sin gimnasio, con cuenta). Plataforma.
    /// Cubre aislamiento entre gimnasios, idempotencia (asistencia diaria y generacion de cuotas) y morosidad derivada.
    /// </summary>
    public class AsistenciaCuotasTests : IClassFixture<AsistenciaCuotasTests.Escenario>
    {
        private readonly Escenario _e;
        public AsistenciaCuotasTests(Escenario e) { _e = e; }

        public sealed record Qr(string Token, DateTime ExpiraUtc, int DuracionSegundos);
        public sealed record AsistenciaDto(Guid Id, Guid SocioId, string SocioNombre, Guid GimnasioId, string GimnasioNombre, string Metodo, DateOnly Fecha);
        public sealed record Registro(bool YaRegistrada, AsistenciaDto Asistencia);
        public sealed record DelDia(DateOnly Fecha, int Total, List<AsistenciaDto> Asistencias);
        public sealed record Generadas(int Creadas, int Existentes);
        public sealed record Cuota(Guid Id, Guid SocioId, string SocioNombre, Guid GimnasioId, int Anio, int Mes, decimal Monto,
            DateOnly Vencimiento, DateOnly? FechaPago, string Estado, bool Vencida, int DiasVencida);

        private static DateOnly Hoy => DiaLocal.Hoy(TimeProvider.System);

        private Task<int> ContarAsistenciasAsync(Guid socioId) => _e.ContarAsync(db => db.Asistencias.IgnoreQueryFilters().CountAsync(a => a.SocioId == socioId));

        // ---------------- Asistencia por QR ----------------

        [Fact]
        public async Task El_socio_obtiene_un_QR_de_dos_minutos_y_el_trainer_lo_valida_una_sola_vez_por_dia()
        {
            var qr = await (await _e.Cliente(_e.UsuarioA).PostAsync("/api/asistencia/qr", null)).Content.ReadFromJsonAsync<Qr>();
            Assert.Equal(120, qr!.DuracionSegundos);
            Assert.InRange((qr.ExpiraUtc - DateTime.UtcNow).TotalSeconds, 100, 125);

            var trainer = _e.Cliente(_e.Trainer1);
            var r1 = await trainer.PostAsJsonAsync("/api/asistencia/qr/validar", new { token = qr.Token });
            Assert.Equal(HttpStatusCode.OK, r1.StatusCode);
            var primero = await r1.Content.ReadFromJsonAsync<Registro>();
            Assert.False(primero!.YaRegistrada);
            Assert.Equal("Qr", primero.Asistencia.Metodo);
            Assert.Equal(_e.SocioA.Id, primero.Asistencia.SocioId);
            Assert.Equal(Hoy, primero.Asistencia.Fecha);

            // Idempotente: el mismo dia devuelve la misma fila, tambien por la via manual.
            var segundo = await (await trainer.PostAsJsonAsync("/api/asistencia/qr/validar", new { token = qr.Token })).Content.ReadFromJsonAsync<Registro>();
            var manual = await (await _e.Cliente(_e.Dueno1).PostAsJsonAsync("/api/asistencia/manual", new { socioId = _e.SocioA.Id })).Content.ReadFromJsonAsync<Registro>();
            Assert.True(segundo!.YaRegistrada);
            Assert.True(manual!.YaRegistrada);
            Assert.Equal(primero.Asistencia.Id, segundo.Asistencia.Id);
            Assert.Equal(primero.Asistencia.Id, manual.Asistencia.Id);
            Assert.Equal(1, await ContarAsistenciasAsync(_e.SocioA.Id));
        }

        [Fact]
        public async Task Un_codigo_alterado_o_vencido_se_rechaza_con_400_y_no_registra_nada()
        {
            var qr = await (await _e.Cliente(_e.UsuarioC).PostAsync("/api/asistencia/qr", null)).Content.ReadFromJsonAsync<Qr>();
            var alterado = (qr!.Token[0] == 'A' ? "B" : "A") + qr.Token[1..];
            var vencido = TokenAsistencia.Generar(_e.SocioC.Id, DateTimeOffset.UtcNow.AddSeconds(-1), GymBroApiFactory.ClaveQrDePrueba);
            var otraClave = TokenAsistencia.Generar(_e.SocioC.Id, DateTimeOffset.UtcNow.AddMinutes(2), "otra-clave-que-no-es-la-del-servidor-000");

            var dueno2 = _e.Cliente(_e.Dueno2);
            foreach (var token in new[] { alterado, vencido, otraClave, "no-es-un-codigo", "" })
            {
                var r = await dueno2.PostAsJsonAsync("/api/asistencia/qr/validar", new { token });
                Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
            }
            var rv = await dueno2.PostAsJsonAsync("/api/asistencia/qr/validar", new { token = vencido });
            Assert.Contains("venció", await rv.Content.ReadAsStringAsync());
            Assert.Equal(0, await ContarAsistenciasAsync(_e.SocioC.Id));
        }

        [Fact]
        public async Task El_codigo_tipeado_con_guiones_y_en_minusculas_tambien_vale()
        {
            var qr = await (await _e.Cliente(_e.UsuarioC).PostAsync("/api/asistencia/qr", null)).Content.ReadFromJsonAsync<Qr>();
            var tipeado = string.Join("-", qr!.Token.ToLowerInvariant().Chunk(4).Select(c => new string(c)));
            var r = await _e.Cliente(_e.Dueno2).PostAsJsonAsync("/api/asistencia/qr/validar", new { token = tipeado });
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            await _e.Api.ConDbAsync(db => db.Asistencias.IgnoreQueryFilters().Where(a => a.SocioId == _e.SocioC.Id).ExecuteDeleteAsync());
        }

        [Fact]
        public async Task Un_socio_sin_gimnasio_no_obtiene_QR()
        {
            var r = await _e.Cliente(_e.UsuarioX).PostAsync("/api/asistencia/qr", null);
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        }

        // ---------------- Aislamiento entre gimnasios ----------------

        [Fact]
        public async Task El_personal_de_otro_gimnasio_no_registra_ni_ve_asistencias_ajenas()
        {
            var qrA = await (await _e.Cliente(_e.UsuarioA).PostAsync("/api/asistencia/qr", null)).Content.ReadFromJsonAsync<Qr>();
            var dueno2 = _e.Cliente(_e.Dueno2);
            Assert.Equal(HttpStatusCode.NotFound, (await dueno2.PostAsJsonAsync("/api/asistencia/qr/validar", new { token = qrA!.Token })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await dueno2.PostAsJsonAsync("/api/asistencia/manual", new { socioId = _e.SocioB.Id })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await dueno2.GetAsync($"/api/asistencia?gimnasioId={_e.G1.Id}")).StatusCode);

            await _e.Cliente(_e.Dueno1).PostAsJsonAsync("/api/asistencia/manual", new { socioId = _e.SocioB.Id });
            var delDia2 = await dueno2.GetFromJsonAsync<DelDia>("/api/asistencia");
            Assert.DoesNotContain(delDia2!.Asistencias, a => a.GimnasioId == _e.G1.Id);
            var delDia1 = await _e.Cliente(_e.Trainer1).GetFromJsonAsync<DelDia>("/api/asistencia");
            Assert.Contains(delDia1!.Asistencias, a => a.SocioId == _e.SocioB.Id && a.Metodo == "Manual" && a.SocioNombre == "Bruno Sintetico");
            Assert.All(delDia1.Asistencias, a => Assert.Equal(_e.G1.Id, a.GimnasioId));
        }

        [Fact]
        public async Task La_plataforma_lee_asistencias_de_todos_los_gimnasios_y_no_registra()
        {
            await _e.Cliente(_e.Dueno1).PostAsJsonAsync("/api/asistencia/manual", new { socioId = _e.SocioB.Id });
            var plataforma = _e.Cliente(_e.Plataforma);
            var todas = await plataforma.GetFromJsonAsync<DelDia>("/api/asistencia");
            Assert.Contains(todas!.Asistencias, a => a.GimnasioId == _e.G1.Id && a.GimnasioNombre == "Gimnasio 1 (asistencia)");
            var soloG2 = await plataforma.GetFromJsonAsync<DelDia>($"/api/asistencia?gimnasioId={_e.G2.Id}");
            Assert.All(soloG2!.Asistencias, a => Assert.Equal(_e.G2.Id, a.GimnasioId));
            Assert.Equal(HttpStatusCode.NotFound, (await plataforma.GetAsync($"/api/asistencia?gimnasioId={Guid.NewGuid()}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await plataforma.PostAsJsonAsync("/api/asistencia/manual", new { socioId = _e.SocioB.Id })).StatusCode);
        }

        [Fact]
        public async Task Registros_simultaneos_del_mismo_socio_dejan_una_sola_fila()
        {
            var clientes = Enumerable.Range(0, 6).Select(i => _e.Cliente(i % 2 == 0 ? _e.Dueno1 : _e.Trainer1)).ToList();
            var respuestas = await Task.WhenAll(clientes.Select(c => c.PostAsJsonAsync("/api/asistencia/manual", new { socioId = _e.SocioI.Id })));
            Assert.All(respuestas, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
            var registros = await Task.WhenAll(respuestas.Select(r => r.Content.ReadFromJsonAsync<Registro>()));
            Assert.Single(registros.Select(r => r!.Asistencia.Id).Distinct());
            Assert.Equal(1, registros.Count(r => !r!.YaRegistrada));
            Assert.Equal(1, await ContarAsistenciasAsync(_e.SocioI.Id));
        }

        // ---------------- Cuotas ----------------

        [Fact]
        public async Task Generar_el_mes_crea_solo_para_activos_y_repetirlo_no_duplica()
        {
            var dueno1 = _e.Cliente(_e.Dueno1);
            var pedido = new { anio = 2030, mes = 1, monto = 15000.50m, vencimiento = "2030-01-10" };
            var g1 = await (await dueno1.PostAsJsonAsync("/api/cuotas/generar", pedido)).Content.ReadFromJsonAsync<Generadas>();
            var g2 = await (await dueno1.PostAsJsonAsync("/api/cuotas/generar", pedido)).Content.ReadFromJsonAsync<Generadas>();
            Assert.Equal(new Generadas(2, 0), g1);   // A y B; I esta inactivo
            Assert.Equal(new Generadas(0, 2), g2);

            var lista = await dueno1.GetFromJsonAsync<List<Cuota>>("/api/cuotas?anio=2030&mes=1");
            Assert.Equal(new[] { _e.SocioA.Id, _e.SocioB.Id }.OrderBy(x => x), lista!.Select(c => c.SocioId).OrderBy(x => x));
            Assert.All(lista, c => Assert.Equal(15000.50m, c.Monto));
            Assert.DoesNotContain(lista, c => c.SocioId == _e.SocioI.Id);
        }

        [Fact]
        public async Task La_morosidad_se_deriva_del_vencimiento_y_el_pago_la_quita()
        {
            var dueno1 = _e.Cliente(_e.Dueno1);
            var ayer = Hoy.AddDays(-1);
            await dueno1.PostAsJsonAsync("/api/cuotas/generar", new { anio = 2029, mes = 6, monto = 1000m, vencimiento = ayer.ToString("yyyy-MM-dd") });
            // Vence hoy: todavia no esta vencida.
            await dueno1.PostAsJsonAsync("/api/cuotas/generar", new { anio = 2029, mes = 7, monto = 1000m, vencimiento = Hoy.ToString("yyyy-MM-dd") });

            var morosos = await dueno1.GetFromJsonAsync<List<Cuota>>("/api/cuotas/morosos");
            var junio = morosos!.Where(c => c.Anio == 2029 && c.Mes == 6).ToList();
            Assert.Equal(2, junio.Count);
            Assert.All(junio, c => { Assert.True(c.Vencida); Assert.Equal("Pendiente", c.Estado); Assert.Equal(1, c.DiasVencida); });
            Assert.DoesNotContain(morosos, c => c.Anio == 2029 && c.Mes == 7);

            var deA = junio.Single(c => c.SocioId == _e.SocioA.Id);
            var pago = await dueno1.PutAsJsonAsync($"/api/cuotas/{deA.Id}/pago", new { });
            Assert.Equal(HttpStatusCode.OK, pago.StatusCode);
            var pagada = await pago.Content.ReadFromJsonAsync<Cuota>();
            Assert.Equal(("Pagada", (DateOnly?)Hoy, false), (pagada!.Estado, pagada.FechaPago, pagada.Vencida));
            Assert.Equal(HttpStatusCode.BadRequest, (await dueno1.PutAsJsonAsync($"/api/cuotas/{deA.Id}/pago", new { })).StatusCode);

            var despues = await dueno1.GetFromJsonAsync<List<Cuota>>("/api/cuotas/morosos");
            Assert.DoesNotContain(despues!, c => c.Id == deA.Id);
            Assert.Contains(despues!, c => c.SocioId == _e.SocioB.Id && c.Anio == 2029 && c.Mes == 6);

            // En la base no hay un estado "vencida": solo Pendiente o Pagada.
            var otros = await _e.LeerAsync(db => db.Database.SqlQueryRaw<MigracionReglasSeguridadTests.Conteo>(
                "SELECT count(*)::int AS cantidad FROM cuotas_socio WHERE estado NOT IN ('Pendiente', 'Pagada')").SingleAsync());
            Assert.Equal(0, otros.Cantidad);
        }

        [Fact]
        public async Task Cuotas_aisladas_entre_gimnasios_y_el_socio_ve_solo_las_suyas()
        {
            await _e.Cliente(_e.Dueno1).PostAsJsonAsync("/api/cuotas/generar", new { anio = 2031, mes = 3, monto = 500m, vencimiento = "2031-03-10" });
            await _e.Cliente(_e.Dueno2).PostAsJsonAsync("/api/cuotas/generar", new { anio = 2031, mes = 3, monto = 700m, vencimiento = "2031-03-10" });

            var deG2 = await _e.Cliente(_e.Dueno2).GetFromJsonAsync<List<Cuota>>("/api/cuotas?anio=2031&mes=3");
            Assert.Equal(new[] { _e.SocioC.Id }, deG2!.Select(c => c.SocioId));
            Assert.Equal(HttpStatusCode.NotFound, (await _e.Cliente(_e.Dueno2).GetAsync($"/api/cuotas?anio=2031&mes=3&gimnasioId={_e.G1.Id}")).StatusCode);

            var deG1 = await _e.Cliente(_e.Dueno1).GetFromJsonAsync<List<Cuota>>("/api/cuotas?anio=2031&mes=3");
            var ajena = deG1!.First();
            Assert.Equal(HttpStatusCode.NotFound, (await _e.Cliente(_e.Dueno2).PutAsJsonAsync($"/api/cuotas/{ajena.Id}/pago", new { })).StatusCode);

            var mias = await _e.Cliente(_e.UsuarioA).GetFromJsonAsync<List<Cuota>>("/api/cuotas/mias");
            Assert.NotEmpty(mias!);
            Assert.All(mias!, c => Assert.Equal(_e.SocioA.Id, c.SocioId));

            var plataforma = _e.Cliente(_e.Plataforma);
            var todas = await plataforma.GetFromJsonAsync<List<Cuota>>("/api/cuotas?anio=2031&mes=3");
            Assert.Contains(todas!, c => c.GimnasioId == _e.G1.Id);
            Assert.Contains(todas!, c => c.GimnasioId == _e.G2.Id);
            Assert.Equal(HttpStatusCode.Forbidden, (await plataforma.PostAsJsonAsync("/api/cuotas/generar", new { anio = 2031, mes = 4, monto = 1m, vencimiento = "2031-04-10" })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await plataforma.PutAsJsonAsync($"/api/cuotas/{ajena.Id}/pago", new { })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await _e.Cliente(_e.Trainer1).GetAsync("/api/cuotas?anio=2031&mes=3")).StatusCode);
        }

        [Theory]
        [InlineData(1999, 1, 10, "2030-01-10")]
        [InlineData(2030, 13, 10, "2030-01-10")]
        [InlineData(2030, 1, 0, "2030-01-10")]
        [InlineData(2030, 1, 10.555, "2030-01-10")]
        [InlineData(2030, 1, 10, null)]
        public async Task Generar_con_datos_invalidos_responde_400_sin_efectos(int anio, int mes, double monto, string? vencimiento)
        {
            var r = await _e.Cliente(_e.Dueno1).PostAsJsonAsync("/api/cuotas/generar", new { anio, mes, monto = (decimal)monto, vencimiento });
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        }

        // ---------------- Migracion ----------------

        [Fact]
        public async Task La_migracion_tiene_Down_y_Up_reversibles()
        {
            var tablas = "SELECT count(*)::int AS cantidad FROM information_schema.tables WHERE table_name IN ('asistencias', 'cuotas_socio')";
            int trasDown = -1, trasUp = -1;
            var api = new GymBroApiFactory();
            await api.InitializeAsync();
            try
            {
                await api.ConDbAsync(async db =>
                {
                    var migrador = db.GetService<IMigrator>();
                    await migrador.MigrateAsync("20261124133430_Etapa7d_ProtocoloPiernas");
                    trasDown = (await db.Database.SqlQueryRaw<MigracionReglasSeguridadTests.Conteo>(tablas).SingleAsync()).Cantidad;
                    await migrador.MigrateAsync();
                    trasUp = (await db.Database.SqlQueryRaw<MigracionReglasSeguridadTests.Conteo>(tablas).SingleAsync()).Cantidad;
                });
            }
            finally
            {
                await api.DisposeAsync();
            }
            Assert.Equal((0, 2), (trasDown, trasUp));
        }

        /// <summary>API + datos sinteticos compartidos.</summary>
        public class Escenario : IAsyncLifetime
        {
            public GymBroApiFactory Api { get; } = new();
            public Gimnasio G1 { get; } = new() { Nombre = "Gimnasio 1 (asistencia)", Codigo = "ASIS-1" };
            public Gimnasio G2 { get; } = new() { Nombre = "Gimnasio 2 (asistencia)", Codigo = "ASIS-2" };
            public Socio SocioA { get; private set; } = null!;
            public Socio SocioB { get; private set; } = null!;
            public Socio SocioI { get; private set; } = null!;
            public Socio SocioC { get; private set; } = null!;
            public Socio SocioX { get; private set; } = null!;
            public Usuario Dueno1 { get; private set; } = null!;
            public Usuario Trainer1 { get; private set; } = null!;
            public Usuario Dueno2 { get; private set; } = null!;
            public Usuario UsuarioA { get; private set; } = null!;
            public Usuario UsuarioC { get; private set; } = null!;
            public Usuario UsuarioX { get; private set; } = null!;
            public Usuario Plataforma { get; private set; } = null!;

            public HttpClient Cliente(Usuario u) => Api.ClienteComo(u);

            public async Task<int> ContarAsync(Func<GymBro.Infrastructure.Persistence.GymBroDbContext, Task<int>> consulta)
            {
                var n = 0;
                await Api.ConDbAsync(async db => n = await consulta(db));
                return n;
            }

            public async Task<T> LeerAsync<T>(Func<GymBro.Infrastructure.Persistence.GymBroDbContext, Task<T>> consulta)
            {
                T valor = default!;
                await Api.ConDbAsync(async db => valor = await consulta(db));
                return valor;
            }

            public async Task InitializeAsync()
            {
                await Api.InitializeAsync();
                SocioA = NuevoSocio("Ana", G1.Id);
                SocioB = NuevoSocio("Bruno", G1.Id);
                SocioI = NuevoSocio("Ines", G1.Id, EstadoSocio.Inactivo);
                SocioC = NuevoSocio("Carla", G2.Id);
                SocioX = NuevoSocio("Xavier", null);
                Dueno1 = Personal("dueno1", RolUsuario.AdministradorGimnasio, G1.Id);
                Trainer1 = Personal("trainer1", RolUsuario.Entrenador, G1.Id);
                Dueno2 = Personal("dueno2", RolUsuario.AdministradorGimnasio, G2.Id);
                UsuarioA = CuentaSocio(SocioA);
                UsuarioC = CuentaSocio(SocioC);
                UsuarioX = CuentaSocio(SocioX);
                Plataforma = new Usuario { Email = "plataforma@asistencia.test", NombreCompleto = "Plataforma", Rol = RolUsuario.SuperAdministrador, PasswordHash = "-" };

                await Api.ConDbAsync(db =>
                {
                    db.Gimnasios.AddRange(G1, G2);
                    db.Socios.AddRange(SocioA, SocioB, SocioI, SocioC, SocioX);
                    db.Usuarios.AddRange(Dueno1, Trainer1, Dueno2, UsuarioA, UsuarioC, UsuarioX, Plataforma);
                    return Task.CompletedTask;
                });
            }

            public async Task DisposeAsync() => await Api.DisposeAsync();

            private static Socio NuevoSocio(string nombre, Guid? gimnasioId, EstadoSocio estado = EstadoSocio.Activo) => new()
            {
                Id = Guid.NewGuid(), Nombre = nombre, Apellido = "Sintetico", Email = $"{nombre.ToLowerInvariant()}@asistencia.test", Genero = "F",
                FechaNacimiento = new DateTime(1995, 1, 1, 0, 0, 0, DateTimeKind.Utc), GimnasioId = gimnasioId, Estado = estado,
                Objetivo = ObjetivoEntrenamiento.Mantenimiento, Nivel = NivelExperiencia.Principiante, OrigenDatos = OrigenDatos.Sintetico,
            };

            private static Usuario Personal(string nombre, RolUsuario rol, Guid gimnasioId) =>
                new() { Email = $"{nombre}@asistencia.test", NombreCompleto = nombre, Rol = rol, GimnasioId = gimnasioId, PasswordHash = "-" };

            private static Usuario CuentaSocio(Socio s) =>
                new() { Email = s.Email, NombreCompleto = s.NombreCompleto, Rol = RolUsuario.Socio, SocioId = s.Id, PasswordHash = "-" };
        }
    }
}
