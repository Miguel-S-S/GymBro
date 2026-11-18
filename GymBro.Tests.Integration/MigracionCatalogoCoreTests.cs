using GymBro.Domain.Enums;
using GymBro.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace GymBro.Tests.Integration
{
    /// <summary>
    /// Etapa 9a-1: la migración Etapa9a1_CatalogoCore sobre filas con la forma real de gymbro-db (17/11/2026):
    /// 6 filas 'Abs' en zona 'Legs', una de ellas la plancha de 30-60 series x 1 repetición.
    /// La base del contenedor se vuelve a la migración anterior, se insertan las filas SINTÉTICAS y se aplica la migración.
    /// </summary>
    public class MigracionCatalogoCoreTests : IClassFixture<MigracionCatalogoCoreTests.Escenario>
    {
        private const string MigracionAnterior = "20261117230626_Fix_EnumsNoDefinidos";
        private readonly Escenario _e;
        public MigracionCatalogoCoreTests(Escenario e) { _e = e; }

        public sealed record Fila(Guid Id, string ParteCuerpo, string SubzonaObjetivo, string ZonaObjetivo, int SeriesMinimas, int SeriesMaximas,
            int RepeticionesMinimas, int RepeticionesMaximas, DateTime? FechaBaja, string TipoMedida);

        [Fact]
        public void Las_filas_de_abdominales_pasan_a_Core_y_la_plancha_30_60_x_1_queda_dada_de_baja()
        {
            Assert.Equal(6, _e.Filas.Count(f => f.ParteCuerpo == "Abs"));
            Assert.All(_e.Filas.Where(f => f.ParteCuerpo == "Abs"), f => Assert.Equal("Core", f.ZonaObjetivo));
            Assert.All(_e.Filas, f => Assert.Equal("Repeticiones", f.TipoMedida));

            var plancha = Assert.Single(_e.Filas, f => f.SeriesMinimas == 30);
            Assert.NotNull(plancha.FechaBaja);
            Assert.All(_e.Filas.Where(f => f.SeriesMinimas != 30), f => Assert.Null(f.FechaBaja));

            // Las filas de piernas no se tocan.
            Assert.All(_e.Filas.Where(f => f.ParteCuerpo == "Legs"), f => Assert.Equal("Legs", f.ZonaObjetivo));
        }

        [Fact]
        public async Task La_dosis_de_Piernas_ya_no_depende_de_la_exclusion_por_abs_y_no_se_contamina()
        {
            await _e.Api.ConDbAsync(async db =>
            {
                var repo = new BaselineRepository(db);

                var piernas = await repo.BuscarDosificacionAsync(new[] { "Legs", "Piernas" }, TipoMedida.Repeticiones);
                // Orden fijo (parte, subzona, id): Legs/Calves precede a Legs/Quadriceps, como antes de la etapa.
                // Lo que se verifica es que la fila elegida es de piernas y nunca una de abdominales.
                Assert.Equal(("Legs", "Calves", 15, 20), (piernas!.ParteCuerpo, piernas.SubzonaObjetivo, piernas.RepeticionesMinimas, piernas.RepeticionesMaximas));

                // Core por repeticiones: la primera fila Abs vigente (orden parte, subzona, id), nunca la plancha dada de baja.
                var core = await repo.BuscarDosificacionAsync(new[] { "Core", "Abs", "Abdominales" }, TipoMedida.Repeticiones);
                Assert.Equal(("Abs", 3, 4, 10, 15), (core!.ParteCuerpo, core.SeriesMinimas, core.SeriesMaximas, core.RepeticionesMinimas, core.RepeticionesMaximas));
            });
        }

        [Fact]
        public async Task El_SQL_es_idempotente_si_no_hay_filas_afectadas_no_hace_nada()
        {
            int afectadas = -1;
            await _e.Api.ConDbAsync(async db => afectadas = await db.Database.ExecuteSqlRawAsync(
                "UPDATE dosificaciones SET zona_objetivo = 'Core' WHERE lower(parte_cuerpo) IN ('abs', 'abdominales') AND zona_objetivo <> 'Core';"));
            Assert.Equal(0, afectadas);
        }

        public class Escenario : IAsyncLifetime
        {
            public GymBroApiFactory Api { get; } = new();
            public List<Fila> Filas { get; private set; } = new();

            public async Task InitializeAsync()
            {
                await Api.InitializeAsync(); // arranque: todas las migraciones + seeders

                await Api.ConDbAsync(async db =>
                {
                    var migrador = db.GetService<IMigrator>();
                    await migrador.MigrateAsync(MigracionAnterior); // ejecuta el Down de Etapa9a1_CatalogoCore

                    // Filas SINTÉTICAS con la forma de las reales (parte, subzona, zona, series y repeticiones).
                    await db.Database.ExecuteSqlRawAsync(@"
INSERT INTO dosificaciones (id, parte_cuerpo, subzona_objetivo, zona_objetivo, series_minimas, series_maximas, repeticiones_minimas, repeticiones_maximas, fecha_alta, usuario_alta) VALUES
 (gen_random_uuid(), 'Abs', 'Lower',  'Legs', 30, 60, 1, 1, now(), 1),
 (gen_random_uuid(), 'Abs', 'Lower',  'Legs', 3, 4, 10, 15, now(), 1),
 (gen_random_uuid(), 'Abs', 'Middle', 'Legs', 3, 4, 10, 15, now(), 1),
 (gen_random_uuid(), 'Abs', 'Middle', 'Legs', 3, 4, 10, 15, now(), 1),
 (gen_random_uuid(), 'Abs', 'Upper',  'Legs', 3, 4, 10, 15, now(), 1),
 (gen_random_uuid(), 'Abs', 'Upper',  'Legs', 3, 4, 10, 15, now(), 1),
 (gen_random_uuid(), 'Legs', 'Quadriceps', 'Legs', 3, 4, 8, 12, now(), 1),
 (gen_random_uuid(), 'Legs', 'Calves', 'Legs', 3, 15, 15, 20, now(), 1);");

                    await migrador.MigrateAsync(); // vuelve a aplicar Etapa9a1_CatalogoCore
                });

                await Api.ConDbAsync(async db => Filas = await db.Database.SqlQueryRaw<Fila>(
                    @"SELECT id, parte_cuerpo, subzona_objetivo, zona_objetivo, series_minimas, series_maximas, repeticiones_minimas,
                             repeticiones_maximas, fecha_baja, tipo_medida
                      FROM dosificaciones").ToListAsync());
            }

            public async Task DisposeAsync() => await Api.DisposeAsync();
        }
    }
}
