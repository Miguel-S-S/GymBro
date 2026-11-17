using GymBro.Api.Infrastructure;
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

        /// <summary>API en un entorno dado, con un series.csv sintético en su carpeta de datasets.</summary>
        public abstract class ApiConSeriesCsv : GymBroApiFactory
        {
            public override async Task InitializeAsync()
            {
                await File.WriteAllTextAsync(Path.Combine(CarpetaDatasets, "series.csv"), SeriesCsvSintetico);
                await base.InitializeAsync();
            }

            public async Task<int> ContarDosificacionesAsync()
            {
                var cantidad = 0;
                await ConDbAsync(async db => cantidad = await db.Dosificaciones.CountAsync());
                return cantidad;
            }
        }

        public class ApiProduccion : ApiConSeriesCsv
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
