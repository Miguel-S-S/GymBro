using GymBro.Api.Infrastructure;
using GymBro.Domain.Enums;
using GymBro.Infrastructure.Persistence.Seeders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace GymBro.Tests.Integration
{
    /// <summary>
    /// Etapa 8c, T3: las migraciones se aplican siempre y los datasets CSV solo se siembran si
    /// Datasets:SembrarAlIniciar es verdadero (por defecto, solo en Development).
    /// Las dos APIs reciben la misma carpeta con un series.csv; solo cambia el entorno o la clave.
    /// Etapa 9a-1: el catálogo propio del proyecto (Core, CatalogoProyectoSeeder) se siembra siempre; los conteos
    /// "del dataset" excluyen sus filas para seguir verificando lo mismo que en la Etapa 8c.
    /// </summary>
    public class SiembraDatasetsTests
    {
        /// <summary>
        /// series.csv SINTÉTICO de prueba, con los encabezados que espera DosificacionMap.
        /// (El series.csv versionado en GymBro.IA/gymbro_datasets tiene encabezados en español y el seeder actual no lo carga:
        /// hallazgo informado en la Etapa 8c, fuera de su alcance.)
        /// </summary>
        private const string SeriesCsvSintetico =
            "Body_Part,Target_Subzone,Target_Zone,Min_Sets,Max_Sets,Min_Reps,Max_Reps\n" +
            "Pecho,Parte superior del pecho,Pecho,3,4,8,12\n";

        /// <summary>gym_exercise_dataset.csv SINTÉTICO de prueba, con los encabezados que espera EjercicioMusculacionMap.</summary>
        private const string EjerciciosCsvSintetico =
            "Exercise Name,Equipment,Target_Zone,Main_muscle,Synergist_Muscles,Stabilizer_Muscles,Preparation,Execution,Difficulty (1-5)\n" +
            "Press sintetico,Barbell,Chest,Pectoral,Triceps,Ninguno,Preparacion de prueba,Ejecucion de prueba,2\n" +
            "Remo sintetico,Dumbbell,Back,Dorsal,Biceps,Ninguno,Preparacion de prueba,Ejecucion de prueba,2\n";

        /// <summary>API en un entorno dado, con un series.csv sintético en su carpeta de datasets.</summary>
        public abstract class ApiConSeriesCsv : GymBroApiFactory
        {
            public override async Task InitializeAsync()
            {
                await File.WriteAllTextAsync(Path.Combine(CarpetaDatasets, "series.csv"), SeriesCsvSintetico);
                await File.WriteAllTextAsync(Path.Combine(CarpetaDatasets, "gym_exercise_dataset.csv"), EjerciciosCsvSintetico);
                await base.InitializeAsync();
            }

            /// <summary>Dosificaciones del dataset (sin las filas propias del proyecto).</summary>
            public async Task<int> ContarDosificacionesAsync()
            {
                var ids = CatalogoProyectoSeeder.IdsDosificacionesProyecto;
                var cantidad = 0;
                await ConDbAsync(async db => cantidad = await db.Dosificaciones.CountAsync(d => !ids.Contains(d.Id)));
                return cantidad;
            }

            /// <summary>(ejercicios del dataset, ejercicios del proyecto, dosificaciones del proyecto).</summary>
            public async Task<(int Dataset, int Proyecto, int DosisProyecto)> ContarCatalogoAsync()
            {
                var ids = CatalogoProyectoSeeder.IdsDosificacionesProyecto;
                (int, int, int) r = default;
                await ConDbAsync(async db => r = (
                    await db.Ejercicios.CountAsync(e => e.Origen == OrigenEjercicio.Dataset),
                    await db.Ejercicios.CountAsync(e => e.Origen == OrigenEjercicio.Proyecto),
                    await db.Dosificaciones.CountAsync(d => ids.Contains(d.Id))));
                return r;
            }

            /// <summary>Ejecuta DataSeeder sobre la misma base, como lo haría un arranque con los datasets habilitados.</summary>
            public Task SembrarDatasetsAsync() => ConDbAsync(db => DataSeeder.SeedAsync(db, CarpetaDatasets));

            /// <summary>Ejecuta CatalogoProyectoSeeder sobre la misma base, como lo haría otro arranque.</summary>
            public async Task<(int, int)> SembrarCatalogoProyectoAsync()
            {
                (int, int) r = default;
                await ConDbAsync(async db => r = await CatalogoProyectoSeeder.SeedAsync(db));
                return r;
            }
        }

        public class ApiProduccion : ApiConSeriesCsv
        {
            protected override string Entorno => "Production";
        }

        /// <summary>Otra API en Production (base propia): se le habilitan los datasets después del primer arranque.</summary>
        public class ApiProduccionCargaPosterior : ApiConSeriesCsv
        {
            protected override string Entorno => "Production";
        }

        public class ApiConSiembraForzada : ApiConSeriesCsv
        {
            protected override void ConfigurarExtra(IWebHostBuilder builder) =>
                builder.UseSetting(ArranqueDatos.ClaveSembrarAlIniciar, "true");
        }

        public class EnProduccion : IClassFixture<ApiProduccion>
        {
            private readonly ApiProduccion _api;
            public EnProduccion(ApiProduccion api) { _api = api; }

            [Fact]
            public async Task No_lee_los_CSV_pero_aplica_las_migraciones()
            {
                // La consulta funciona: la tabla existe porque las migraciones se aplicaron.
                Assert.Equal(0, await _api.ContarDosificacionesAsync());
            }

            [Fact]
            public async Task Siembra_el_catalogo_del_proyecto_aunque_no_siembre_datasets()
            {
                // Etapa 9a-1: Datasets:SembrarAlIniciar es falso en Production, pero Core se siembra igual.
                Assert.Equal((0, 8, 2), await _api.ContarCatalogoAsync());
            }
        }

        /// <summary>(a) Base vacía, Core sembrado y después datasets habilitados: el dataset se carga igual.</summary>
        public class CoreAntesQueLosDatasets : IClassFixture<ApiProduccionCargaPosterior>
        {
            private readonly ApiProduccionCargaPosterior _api;
            public CoreAntesQueLosDatasets(ApiProduccionCargaPosterior api) { _api = api; }

            [Fact]
            public async Task El_catalogo_y_la_dosificacion_del_dataset_se_cargan_aunque_ya_exista_Core()
            {
                Assert.Equal((0, 8, 2), await _api.ContarCatalogoAsync());
                Assert.Equal(0, await _api.ContarDosificacionesAsync());

                await _api.SembrarDatasetsAsync();

                Assert.Equal((2, 8, 2), await _api.ContarCatalogoAsync());
                Assert.Equal(1, await _api.ContarDosificacionesAsync());
            }
        }

        public class ConClaveVerdadera : IClassFixture<ApiConSiembraForzada>
        {
            private readonly ApiConSiembraForzada _api;
            public ConClaveVerdadera(ApiConSiembraForzada api) { _api = api; }

            [Fact]
            public async Task Siembra_los_CSV_fuera_de_Development()
            {
                Assert.True(await _api.ContarDosificacionesAsync() > 0);
            }

            [Fact]
            public async Task Con_los_datasets_cargados_el_catalogo_del_proyecto_no_duplica_ni_pisa_nada()
            {
                // (b) Arranque: DataSeeder y luego CatalogoProyectoSeeder.
                Assert.Equal((2, 8, 2), await _api.ContarCatalogoAsync());
                Assert.Equal(1, await _api.ContarDosificacionesAsync());

                // Otro arranque (los dos seeders de nuevo): mismas filas.
                await _api.SembrarDatasetsAsync();
                Assert.Equal((0, 0), await _api.SembrarCatalogoProyectoAsync());

                Assert.Equal((2, 8, 2), await _api.ContarCatalogoAsync());
                Assert.Equal(1, await _api.ContarDosificacionesAsync());
            }
        }

        [Theory]
        [InlineData("Development", null, true)]
        [InlineData("Production", null, false)]
        [InlineData("Staging", null, false)]
        [InlineData("Testing", null, false)]
        [InlineData("Development", "false", false)]
        [InlineData("Production", "true", true)]
        public void Valor_por_defecto_segun_entorno(string entorno, string? valor, bool esperado)
        {
            var datos = new Dictionary<string, string?>();
            if (valor is not null) datos[ArranqueDatos.ClaveSembrarAlIniciar] = valor;
            var configuracion = new ConfigurationBuilder().AddInMemoryCollection(datos).Build();

            Assert.Equal(esperado, ArranqueDatos.DebeSembrarDatasets(configuracion, new Entorno(entorno)));
        }

        private sealed class Entorno(string nombre) : IHostEnvironment
        {
            public string EnvironmentName { get; set; } = nombre;
            public string ApplicationName { get; set; } = "GymBro.Api";
            public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
            public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        }
    }
}
