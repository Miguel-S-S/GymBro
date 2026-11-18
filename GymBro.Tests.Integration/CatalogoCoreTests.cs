using System.Net;
using System.Net.Http.Json;
using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Domain.Exceptions;
using GymBro.Infrastructure.Persistence.Seeders;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace GymBro.Tests.Integration
{
    /// <summary>
    /// Etapa 9a-1: catálogo de Core del proyecto, tipo de medida y baseline con cinco zonas, contra la API real y PostgreSQL.
    /// Escenario SINTÉTICO de prueba: un gimnasio, un socio con consentimiento y medición, y un ejercicio del dataset
    /// (también sintético) por cada zona de Pecho, Espalda, Piernas y Brazos. Core lo aporta CatalogoProyectoSeeder.
    /// </summary>
    public class CatalogoCoreTests
    {
        // ---- Escenarios ----

        public class ApiProduccion : GymBroApiFactory
        {
            protected override string Entorno => "Production";
        }

        /// <summary>API + gimnasio + socio con consentimiento y medición. Si conZonasDataset, agrega las otras cuatro zonas.</summary>
        public abstract class EscenarioBase<TApi> : IAsyncLifetime where TApi : GymBroApiFactory, new()
        {
            public TApi Api { get; } = new();
            public Socio Socio { get; private set; } = null!;
            public Usuario UsuarioSocio { get; private set; } = null!;
            public Usuario SuperAdmin { get; private set; } = null!;
            protected abstract bool ConZonasDataset { get; }

            public async Task InitializeAsync()
            {
                await Api.InitializeAsync();
                var g = new Gimnasio { Nombre = "Gimnasio (prueba Core)", Codigo = "CORE-1" };
                Socio = new Socio
                {
                    Id = Guid.NewGuid(), Nombre = "Socio", Apellido = "Core", Email = "socio@core.test", Genero = "F",
                    FechaNacimiento = new DateTime(1995, 1, 1, 0, 0, 0, DateTimeKind.Utc), GimnasioId = g.Id,
                    Objetivo = ObjetivoEntrenamiento.Mantenimiento, Nivel = NivelExperiencia.Principiante,
                    ConsentimientoDatosSaludFecha = DateTime.UtcNow,
                };
                UsuarioSocio = new Usuario { Email = Socio.Email, NombreCompleto = "Socio Core", Rol = RolUsuario.Socio, SocioId = Socio.Id, PasswordHash = "-" };
                SuperAdmin = new Usuario { Email = "admin@core.test", NombreCompleto = "Administrador", Rol = RolUsuario.SuperAdministrador, PasswordHash = "-" };

                await Api.ConDbAsync(db =>
                {
                    db.Gimnasios.Add(g);
                    db.Socios.Add(Socio);
                    db.Usuarios.AddRange(UsuarioSocio, SuperAdmin);
                    db.MedicionesFisicas.Add(new MedicionFisica { SocioId = Socio.Id, PesoKg = 65, AlturaCm = 168, IMC = 23m });
                    if (ConZonasDataset)
                    {
                        foreach (var zona in new[] { "Chest", "Back", "Legs", "Arms" })
                        {
                            db.Ejercicios.Add(new Ejercicio
                            {
                                Id = Guid.NewGuid(), Nombre = $"Ejercicio sintetico {zona}", Equipamiento = "Barbell", ZonaObjetivo = zona,
                                MusculoPrincipal = zona, Dificultad = 1,
                            });
                            db.Dosificaciones.Add(new DosificacionVolumetrica
                            {
                                Id = Guid.NewGuid(), ParteCuerpo = zona, SubzonaObjetivo = "General", ZonaObjetivo = zona,
                                SeriesMinimas = 3, SeriesMaximas = 4, RepeticionesMinimas = 8, RepeticionesMaximas = 12,
                            });
                        }
                    }
                    return Task.CompletedTask;
                });
            }

            public async Task<Rutina> GenerarBaselineAsync()
            {
                var r = await Api.ClienteComo(UsuarioSocio).PostAsync($"/api/Socio/{Socio.Id}/prescripciones/baseline", null);
                Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync());
                return (await r.Content.ReadFromJsonAsync<Rutina>())!;
            }

            public async Task DisposeAsync() => await Api.DisposeAsync();
        }

        public class EscenarioCincoZonas : EscenarioBase<GymBroApiFactory> { protected override bool ConZonasDataset => true; }
        public class EscenarioProduccionSoloCore : EscenarioBase<ApiProduccion> { protected override bool ConZonasDataset => false; }

        public sealed record EjercicioRutina(string Ejercicio, string TipoMedida, int SeriesObjetivo, int RepeticionesMinimas, int RepeticionesMaximas, string? Observaciones);
        public sealed record Rutina(Guid Id, string? Observaciones, List<EjercicioRutina> Ejercicios);
        private sealed record ItemCatalogo(Guid Id, string Nombre, string ZonaObjetivo, string Equipamiento, int Dificultad, string TipoMedida, string Origen);
        private sealed record Pagina(List<ItemCatalogo> Items, int Total);

        // ---- Catálogo, seeder y enums ----

        public class Catalogo : IClassFixture<EscenarioCincoZonas>
        {
            private readonly EscenarioCincoZonas _e;
            public Catalogo(EscenarioCincoZonas e) { _e = e; }

            [Fact]
            public async Task El_seeder_carga_los_ocho_ejercicios_de_Core_con_su_medida_y_sin_embedding()
            {
                List<Ejercicio> core = new();
                await _e.Api.ConDbAsync(async db => core = await db.Ejercicios.Where(e => e.Origen == OrigenEjercicio.Proyecto).ToListAsync());

                Assert.Equal(8, core.Count);
                Assert.All(core, e => { Assert.Equal("Core", e.ZonaObjetivo); Assert.Null(e.Embedding); });
                Assert.Equal(new[] { "Bote (boat)", "Plancha" },
                    core.Where(e => e.TipoMedida == TipoMedida.Segundos).Select(e => e.Nombre).OrderBy(n => n));
                Assert.Equal("Cable", core.Single(e => e.Nombre == "Crunch en polea").Equipamiento);
                Assert.All(core.Where(e => e.Nombre != "Crunch en polea"), e => Assert.Equal("Body Weight", e.Equipamiento));
            }

            [Fact]
            public async Task Las_zonas_incluyen_Core_y_el_catalogo_expone_tipoMedida_y_origen()
            {
                // El Administrador de la Plataforma sigue viendo el catálogo completo (sin datos de salud).
                var cliente = _e.Api.ClienteComo(_e.SuperAdmin);

                var zonas = await cliente.GetFromJsonAsync<List<string>>("/api/Ejercicios/zonas");
                Assert.Contains("Core", zonas!);

                var pagina = await cliente.GetFromJsonAsync<Pagina>("/api/Ejercicios?zona=Core&tamanioPagina=100");
                Assert.Equal(8, pagina!.Total);
                Assert.All(pagina.Items, i => Assert.Equal("Proyecto", i.Origen));
                Assert.Equal("Segundos", pagina.Items.Single(i => i.Nombre == "Plancha").TipoMedida);
                Assert.Equal("Repeticiones", pagina.Items.Single(i => i.Nombre == "Crunch abdominal").TipoMedida);

                var dataset = await cliente.GetFromJsonAsync<Pagina>("/api/Ejercicios?zona=Chest");
                Assert.All(dataset!.Items, i => Assert.Equal(("Dataset", "Repeticiones"), (i.Origen, i.TipoMedida)));
            }

            [Fact]
            public async Task El_seeder_es_idempotente_y_no_pisa_una_edicion_ni_reactiva_una_baja()
            {
                var plancha = Guid.Parse("13e90ee5-9fb4-47bf-9abc-1277eae966d1");
                var bote = Guid.Parse("f5854654-9b06-45d2-8b26-79074712d9d7");
                await _e.Api.ConDbAsync(async db =>
                {
                    var p = await db.Ejercicios.SingleAsync(e => e.Id == plancha);
                    p.Nombre = "Plancha frontal (editada por el autor)";
                    var dosis = await db.Dosificaciones.SingleAsync(d => d.Id == CatalogoProyectoSeeder.DosisCoreSegundosId);
                    dosis.RepeticionesMaximas = 45;
                });

                (int, int) agregados = (-1, -1);
                await _e.Api.ConDbAsync(async db => agregados = await CatalogoProyectoSeeder.SeedAsync(db));
                Assert.Equal((0, 0), agregados);

                // (c) Otro arranque: mismas filas.
                await _e.Api.ConDbAsync(async db => agregados = await CatalogoProyectoSeeder.SeedAsync(db));
                Assert.Equal((0, 0), agregados);

                string nombre = ""; int max = 0, proyecto = 0, dosisProyecto = 0;
                await _e.Api.ConDbAsync(async db =>
                {
                    nombre = (await db.Ejercicios.SingleAsync(e => e.Id == plancha)).Nombre;
                    max = (await db.Dosificaciones.SingleAsync(d => d.Id == CatalogoProyectoSeeder.DosisCoreSegundosId)).RepeticionesMaximas;
                    proyecto = await db.Ejercicios.IgnoreQueryFilters().CountAsync(e => e.Origen == OrigenEjercicio.Proyecto);
                    dosisProyecto = await db.Dosificaciones.IgnoreQueryFilters().CountAsync(d => d.ParteCuerpo == "Core");
                });
                Assert.Equal("Plancha frontal (editada por el autor)", nombre);
                Assert.Equal(45, max);
                Assert.Equal(8, proyecto);
                Assert.Equal(2, dosisProyecto);

                // Una baja lógica no se reactiva ni se vuelve a insertar.
                await _e.Api.ConDbAsync(async db => (await db.Ejercicios.SingleAsync(e => e.Id == bote)).FechaBaja = DateTime.UtcNow);
                await _e.Api.ConDbAsync(async db => agregados = await CatalogoProyectoSeeder.SeedAsync(db));
                Assert.Equal((0, 0), agregados);
                var vigente = true;
                await _e.Api.ConDbAsync(async db => vigente = await db.Ejercicios.AnyAsync(e => e.Id == bote));
                Assert.False(vigente);

                // Restaura el escenario para las demás pruebas de la clase.
                await _e.Api.ConDbAsync(async db =>
                {
                    var b = await db.Ejercicios.IgnoreQueryFilters().SingleAsync(e => e.Id == bote);
                    b.FechaBaja = null;
                    (await db.Ejercicios.SingleAsync(e => e.Id == plancha)).Nombre = "Plancha";
                    (await db.Dosificaciones.SingleAsync(d => d.Id == CatalogoProyectoSeeder.DosisCoreSegundosId)).RepeticionesMaximas = 40;
                });
            }

            [Fact]
            public async Task La_condicion_de_DataSeeder_por_ids_del_proyecto_se_traduce_a_SQL()
            {
                var ids = CatalogoProyectoSeeder.IdsDosificacionesProyecto;
                var sql = "";
                await _e.Api.ConDbAsync(db =>
                {
                    sql = db.Dosificaciones.Where(d => !ids.Contains(d.Id)).ToQueryString();
                    return Task.CompletedTask;
                });
                Assert.Contains("WHERE", sql);
                Assert.Contains("d.id", sql);
            }

            // ---- Ningún enum nuevo admite un valor no definido (convención de fix-nivel-socio) ----

            [Theory]
            [InlineData(0, 1)]
            [InlineData(3, 1)]
            [InlineData(1, 0)]
            [InlineData(1, 4)]
            public async Task Un_ejercicio_con_tipoMedida_u_origen_no_definido_no_se_guarda(int medida, int origen)
            {
                var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => _e.Api.ConDbAsync(db =>
                {
                    db.Ejercicios.Add(new Ejercicio
                    {
                        Id = Guid.NewGuid(), Nombre = "Invalido", Equipamiento = "Body Weight", ZonaObjetivo = "Core",
                        TipoMedida = (TipoMedida)medida, Origen = (OrigenEjercicio)origen,
                    });
                    return Task.CompletedTask;
                }));
                Assert.StartsWith("Valor no válido para Ejercicio.", ex.Message);
            }

            [Fact]
            public async Task Una_dosificacion_con_tipoMedida_no_definido_no_se_guarda()
            {
                var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => _e.Api.ConDbAsync(db =>
                {
                    db.Dosificaciones.Add(new DosificacionVolumetrica { Id = Guid.NewGuid(), ParteCuerpo = "X", SubzonaObjetivo = "X", ZonaObjetivo = "X", TipoMedida = default });
                    return Task.CompletedTask;
                }));
                Assert.Equal("Valor no válido para DosificacionVolumetrica.TipoMedida.", ex.Message);
            }

            [Theory]
            [InlineData("UPDATE ejercicios SET tipo_medida = '0' WHERE id = {0}", "CK_Ejercicio_TipoMedida")]
            [InlineData("UPDATE ejercicios SET tipo_medida = 'Minutos' WHERE id = {0}", "CK_Ejercicio_TipoMedida")]
            [InlineData("UPDATE ejercicios SET origen = '0' WHERE id = {0}", "CK_Ejercicio_Origen")]
            [InlineData("UPDATE dosificaciones SET tipo_medida = '0' WHERE id = {0}", "CK_Dosificacion_TipoMedida")]
            public async Task La_base_rechaza_valores_no_definidos_de_los_enums_nuevos(string sql, string restriccion)
            {
                var id = sql.Contains("dosificaciones") ? CatalogoProyectoSeeder.DosisCoreRepeticionesId : Guid.Parse("f6bab470-e569-44c2-b48a-f0127ad3b9e9");
                var ex = await Assert.ThrowsAsync<PostgresException>(() => _e.Api.ConDbAsync(db => db.Database.ExecuteSqlRawAsync(sql, id)));
                Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
                Assert.Equal(restriccion, ex.ConstraintName);
            }
        }

        // ---- Baseline ----

        public class BaselineConCincoZonas : IClassFixture<EscenarioCincoZonas>
        {
            private readonly EscenarioCincoZonas _e;
            public BaselineConCincoZonas(EscenarioCincoZonas e) { _e = e; }

            [Fact]
            public async Task Genera_cinco_ejercicios_y_Core_ya_no_figura_como_omitida()
            {
                var rutina = await _e.GenerarBaselineAsync();

                Assert.Equal(5, rutina.Ejercicios.Count);
                Assert.DoesNotContain("omitidas", rutina.Observaciones);

                // Core: el primero por dificultad y nombre es "Crunch abdominal" (dificultad 1, repeticiones): dosis 3 x 10-15.
                var core = rutina.Ejercicios.Single(e => e.Ejercicio == "Crunch abdominal");
                Assert.Equal(("Repeticiones", 3, 10, 15), (core.TipoMedida, core.SeriesObjetivo, core.RepeticionesMinimas, core.RepeticionesMaximas));
                Assert.Null(core.Observaciones);
            }
        }

        /// <summary>(d) Production sin datasets: el baseline usa la dosis de Core del seeder, no la dosis por defecto.</summary>
        public class BaselineEnProduccionSinDatasets : IClassFixture<EscenarioProduccionSoloCore>
        {
            private readonly EscenarioProduccionSoloCore _e;
            public BaselineEnProduccionSinDatasets(EscenarioProduccionSoloCore e) { _e = e; }

            [Fact]
            public async Task Usa_la_dosis_de_Core_del_seeder()
            {
                var rutina = await _e.GenerarBaselineAsync();

                var core = Assert.Single(rutina.Ejercicios);
                Assert.Equal("Crunch abdominal", core.Ejercicio);
                Assert.Equal((3, 10, 15), (core.SeriesObjetivo, core.RepeticionesMinimas, core.RepeticionesMaximas));
                Assert.Null(core.Observaciones); // sin "Sin dosificación en el catálogo ...": no es la dosis por defecto
                Assert.Contains("omitidas: Pecho, Espalda, Piernas, Brazos", rutina.Observaciones);
            }
        }
    }
}
