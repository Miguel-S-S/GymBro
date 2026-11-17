using System.Net;
using System.Net.Http.Json;
using GymBro.Application.Services;
using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Domain.Exceptions;
using GymBro.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using GymBro.Infrastructure.Persistence;
using Xunit;

namespace GymBro.Tests.Integration
{
    /// <summary>
    /// Etapa 8b: bienestar diario contra la API real y PostgreSQL (contenedor descartable).
    /// Escenario SINTÉTICO de prueba:
    ///   Gimnasio 1: socio A (con consentimiento, asignado al trainer), socio B (no asignado), socio S (sin consentimiento), Dueño 1, trainer.
    ///   Gimnasio 2: socio C (con consentimiento) y Dueño 2.
    /// </summary>
    public class BienestarTests : IClassFixture<BienestarTests.Escenario>
    {
        private readonly Escenario _e;
        public BienestarTests(Escenario e) { _e = e; }

        private static DateOnly Hoy => DiaLocal.Hoy(TimeProvider.System);
        private static string Url(DateOnly d) => $"/api/bienestar/{d:yyyy-MM-dd}";
        private static object Cuerpo(short estres = 3, decimal? sueno = 7.5m) =>
            new { horasSueno = sueno, estres, fatiga = (short)2, nutricionCategoria = "Habitual", nutricionNotas = "Dato sintético de prueba" };

        // ---- Alta, actualización y unicidad ----

        [Fact]
        public async Task Dos_PUT_el_mismo_dia_actualizan_y_no_duplican()
        {
            var cliente = _e.Api.ClienteComo(_e.UsuarioDe("A"));

            var r1 = await cliente.PutAsJsonAsync(Url(Hoy), Cuerpo(estres: 3));
            var r2 = await cliente.PutAsJsonAsync(Url(Hoy), Cuerpo(estres: 6));
            Assert.Equal(HttpStatusCode.OK, r1.StatusCode);
            Assert.Equal(HttpStatusCode.OK, r2.StatusCode);
            var d1 = await r1.Content.ReadFromJsonAsync<Registro>();
            var d2 = await r2.Content.ReadFromJsonAsync<Registro>();
            Assert.Equal(d1!.Id, d2!.Id);
            Assert.Equal(6, d2.Estres);

            var filas = 0;
            await _e.Api.ConDbAsync(async db => filas = await db.RegistrosBienestarDiario.CountAsync(r => r.SocioId == _e.Socio("A").Id && r.Fecha == Hoy));
            Assert.Equal(1, filas);

            var hoy = await (await cliente.GetAsync("/api/bienestar/hoy")).Content.ReadFromJsonAsync<RespuestaHoy>();
            Assert.Equal(Hoy, hoy!.FechaLocal);
            Assert.Equal(d2.Id, hoy.Registro!.Id);
        }

        [Fact]
        public async Task El_indice_unico_parcial_impide_dos_filas_vigentes_del_mismo_dia_y_admite_una_dada_de_baja()
        {
            var socio = _e.Socio("B");
            var dia = new DateOnly(2026, 1, 10);
            await _e.Api.ConDbAsync(db =>
            {
                db.RegistrosBienestarDiario.Add(new RegistroBienestarDiario { SocioId = socio.Id, Fecha = dia, Estres = 2, FechaBaja = DateTime.UtcNow });
                db.RegistrosBienestarDiario.Add(new RegistroBienestarDiario { SocioId = socio.Id, Fecha = dia, Estres = 3 });
                return Task.CompletedTask;
            }); // una dada de baja + una vigente: permitido

            var duplicado = () => _e.Api.ConDbAsync(db =>
            {
                db.RegistrosBienestarDiario.Add(new RegistroBienestarDiario { SocioId = socio.Id, Fecha = dia, Estres = 4 });
                return Task.CompletedTask;
            });
            var ex = await Assert.ThrowsAsync<DbUpdateException>(duplicado);
            Assert.Contains("ix_registros_bienestar_diario_socio_id_fecha", ex.InnerException!.Message);
        }

        [Fact]
        public async Task La_carrera_de_dos_altas_del_mismo_dia_se_traduce_a_regla_de_negocio_sin_detalle_interno()
        {
            var socio = _e.Socio("B");
            var dia = new DateOnly(2026, 2, 20);
            await _e.Api.ConDbAsync(db => { db.RegistrosBienestarDiario.Add(new RegistroBienestarDiario { SocioId = socio.Id, Fecha = dia, Fatiga = 1 }); return Task.CompletedTask; });

            using var scope = _e.Api.Services.CreateScope();
            var repo = new RegistroBienestarRepository(scope.ServiceProvider.GetRequiredService<GymBroDbContext>());
            repo.Agregar(new RegistroBienestarDiario { SocioId = socio.Id, Fecha = dia, Fatiga = 5 });

            var ex = await Assert.ThrowsAsync<ReglaNegocioException>(repo.GuardarCambiosAsync);
            Assert.DoesNotContain("23505", ex.Message);
            Assert.DoesNotContain("duplicate", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Restricciones_CHECK_de_la_base_rechazan_estres_fuera_de_1_a_7()
        {
            var socio = _e.Socio("B");
            var invalido = () => _e.Api.ConDbAsync(db =>
            {
                db.RegistrosBienestarDiario.Add(new RegistroBienestarDiario { SocioId = socio.Id, Fecha = new DateOnly(2026, 3, 1), Estres = 8 });
                return Task.CompletedTask;
            });
            var ex = await Assert.ThrowsAsync<DbUpdateException>(invalido);
            Assert.Contains("CK_RegistroBienestar_Estres", ex.InnerException!.Message);
        }

        // ---- Ventana, formato y validación (400 con mensaje de negocio, sin detalle interno) ----

        [Theory]
        [InlineData(-2)]
        [InlineData(1)]
        public async Task Fuera_de_la_ventana_hoy_ayer_responde_400(int dias)
        {
            var r = await _e.Api.ClienteComo(_e.UsuarioDe("A")).PutAsJsonAsync(Url(Hoy.AddDays(dias)), Cuerpo());
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        }

        [Fact]
        public async Task Ayer_se_puede_corregir()
        {
            var r = await _e.Api.ClienteComo(_e.UsuarioDe("A")).PutAsJsonAsync(Url(Hoy.AddDays(-1)), Cuerpo());
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        }

        [Theory]
        [InlineData("17-11-2026")]
        [InlineData("hoy")]
        public async Task Fecha_con_formato_invalido_responde_400(string fecha)
        {
            var r = await _e.Api.ClienteComo(_e.UsuarioDe("A")).PutAsJsonAsync($"/api/bienestar/{fecha}", Cuerpo());
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        }

        [Fact]
        public async Task Estres_fuera_de_rango_responde_400_con_el_motivo_y_sin_detalle_interno()
        {
            var r = await _e.Api.ClienteComo(_e.UsuarioDe("A")).PutAsJsonAsync(Url(Hoy), Cuerpo(estres: 9));
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
            var cuerpo = await r.Content.ReadAsStringAsync();
            Assert.Contains("1 (mejor) a 7 (peor)", cuerpo);
            Assert.DoesNotContain("Exception", cuerpo);
            Assert.DoesNotContain("   at ", cuerpo);
        }

        // ---- Consentimiento (Decisión C) ----

        [Fact]
        public async Task Sin_consentimiento_el_PUT_responde_400_y_no_guarda()
        {
            var r = await _e.Api.ClienteComo(_e.UsuarioDe("S")).PutAsJsonAsync(Url(Hoy), Cuerpo());
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
            var filas = -1;
            await _e.Api.ConDbAsync(async db => filas = await db.RegistrosBienestarDiario.CountAsync(x => x.SocioId == _e.Socio("S").Id));
            Assert.Equal(0, filas);
        }

        [Fact]
        public async Task Sin_consentimiento_el_socio_lee_lista_vacia_y_hoy_sin_registro()
        {
            var cliente = _e.Api.ClienteComo(_e.UsuarioDe("S"));
            var lista = await cliente.GetAsync($"/api/bienestar?socioId={_e.Socio("S").Id}");
            Assert.Equal(HttpStatusCode.OK, lista.StatusCode);
            Assert.Empty((await lista.Content.ReadFromJsonAsync<List<Registro>>())!);

            var hoy = await cliente.GetAsync("/api/bienestar/hoy");
            Assert.Equal(HttpStatusCode.OK, hoy.StatusCode);
            Assert.Null((await hoy.Content.ReadFromJsonAsync<RespuestaHoy>())!.Registro);
        }

        [Fact]
        public async Task Sin_consentimiento_el_Dueno_recibe_400()
        {
            var r = await _e.Api.ClienteComo(_e.UsuarioDe("Dueno1")).GetAsync($"/api/bienestar?socioId={_e.Socio("S").Id}");
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        }

        // ---- Aislamiento (T7: no visible = 404) ----

        [Fact]
        public async Task Un_socio_no_lee_registros_de_un_socio_de_otro_gimnasio()
        {
            await _e.Api.ClienteComo(_e.UsuarioDe("C")).PutAsJsonAsync(Url(Hoy), Cuerpo());
            var r = await _e.Api.ClienteComo(_e.UsuarioDe("A")).GetAsync($"/api/bienestar?socioId={_e.Socio("C").Id}");
            Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
            Assert.DoesNotContain(_e.Socio("C").Id.ToString(), await r.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task Un_socio_no_escribe_registros_de_otro_el_socio_sale_del_JWT()
        {
            var clienteC = _e.Api.ClienteComo(_e.UsuarioDe("C"));
            await clienteC.PutAsJsonAsync(Url(Hoy), Cuerpo(estres: 1));

            // A intenta colar el socioId de C en el cuerpo: se ignora y guarda sobre el suyo.
            var r = await _e.Api.ClienteComo(_e.UsuarioDe("A")).PutAsJsonAsync(Url(Hoy),
                new { socioId = _e.Socio("C").Id, estres = (short)7 });
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            Assert.Equal(_e.Socio("A").Id, (await r.Content.ReadFromJsonAsync<Registro>())!.SocioId);

            var deC = await (await clienteC.GetAsync("/api/bienestar/hoy")).Content.ReadFromJsonAsync<RespuestaHoy>();
            Assert.Equal(1, deC!.Registro!.Estres);
        }

        [Fact]
        public async Task El_Dueno_de_otro_gimnasio_no_ve_al_socio()
        {
            var r = await _e.Api.ClienteComo(_e.UsuarioDe("Dueno2")).GetAsync($"/api/bienestar?socioId={_e.Socio("A").Id}");
            Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        }

        [Fact]
        public async Task El_trainer_ve_a_su_asignado_y_no_a_un_socio_no_asignado()
        {
            await _e.Api.ClienteComo(_e.UsuarioDe("A")).PutAsJsonAsync(Url(Hoy), Cuerpo());
            var trainer = _e.Api.ClienteComo(_e.UsuarioDe("Trainer"));

            var asignado = await trainer.GetAsync($"/api/bienestar?socioId={_e.Socio("A").Id}");
            Assert.Equal(HttpStatusCode.OK, asignado.StatusCode);
            Assert.NotEmpty((await asignado.Content.ReadFromJsonAsync<List<Registro>>())!);

            var noAsignado = await trainer.GetAsync($"/api/bienestar?socioId={_e.Socio("B").Id}");
            Assert.Equal(HttpStatusCode.NotFound, noAsignado.StatusCode);
        }

        private sealed record Registro(Guid Id, Guid SocioId, DateOnly Fecha, int? Estres);
        private sealed record RespuestaHoy(DateOnly FechaLocal, Registro? Registro);

        /// <summary>API + datos sintéticos compartidos.</summary>
        public class Escenario : IAsyncLifetime
        {
            public GymBroApiFactory Api { get; } = new();
            private readonly Dictionary<string, Socio> _socios = new();
            private readonly Dictionary<string, Usuario> _usuarios = new();

            public Socio Socio(string clave) => _socios[clave];
            public Usuario UsuarioDe(string clave) => _usuarios[clave];

            public async Task InitializeAsync()
            {
                await Api.InitializeAsync();
                var g1 = new Gimnasio { Nombre = "Gimnasio 1 (prueba bienestar)", Codigo = "BIEN-1" };
                var g2 = new Gimnasio { Nombre = "Gimnasio 2 (prueba bienestar)", Codigo = "BIEN-2" };

                foreach (var (clave, gimnasio, consentimiento) in new[] { ("A", g1, true), ("B", g1, true), ("S", g1, false), ("C", g2, true) })
                {
                    var s = new Socio
                    {
                        Id = Guid.NewGuid(), Nombre = clave, Apellido = "Test", Email = $"{clave.ToLowerInvariant()}@bienestar.test", Genero = "F",
                        FechaNacimiento = new DateTime(1995, 1, 1, 0, 0, 0, DateTimeKind.Utc), GimnasioId = gimnasio.Id,
                        Objetivo = ObjetivoEntrenamiento.Mantenimiento, Nivel = NivelExperiencia.Principiante,
                        ConsentimientoDatosSaludFecha = consentimiento ? DateTime.UtcNow : null,
                    };
                    _socios[clave] = s;
                    _usuarios[clave] = new Usuario { Email = s.Email, NombreCompleto = $"{clave} Test", Rol = RolUsuario.Socio, SocioId = s.Id, PasswordHash = "-" };
                }
                _usuarios["Dueno1"] = new Usuario { Email = "dueno1@bienestar.test", NombreCompleto = "Dueño 1", Rol = RolUsuario.AdministradorGimnasio, GimnasioId = g1.Id, PasswordHash = "-" };
                _usuarios["Dueno2"] = new Usuario { Email = "dueno2@bienestar.test", NombreCompleto = "Dueño 2", Rol = RolUsuario.AdministradorGimnasio, GimnasioId = g2.Id, PasswordHash = "-" };
                _usuarios["Trainer"] = new Usuario { Email = "trainer@bienestar.test", NombreCompleto = "Trainer", Rol = RolUsuario.Entrenador, GimnasioId = g1.Id, PasswordHash = "-" };

                await Api.ConDbAsync(db =>
                {
                    db.Gimnasios.AddRange(g1, g2);
                    db.Socios.AddRange(_socios.Values);
                    db.Usuarios.AddRange(_usuarios.Values);
                    db.Asignaciones.Add(new AsignacionEntrenadorSocio { GimnasioId = g1.Id, EntrenadorUsuarioId = _usuarios["Trainer"].Id, SocioId = _socios["A"].Id });
                    return Task.CompletedTask;
                });
            }

            public async Task DisposeAsync() => await Api.DisposeAsync();
        }
    }
}
