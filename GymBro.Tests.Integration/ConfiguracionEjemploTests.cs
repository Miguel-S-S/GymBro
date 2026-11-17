using GymBro.Api.Infrastructure;
using GymBro.Api.Security;
using GymBro.Application.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace GymBro.Tests.Integration
{
    /// <summary>
    /// Etapa 8c, T9: GymBro.Api/appsettings.Development.example.json solo lleva claves no secretas
    /// y sus valores se vinculan con las clases de opciones del código.
    /// </summary>
    public class ConfiguracionEjemploTests
    {
        private static IConfigurationRoot Ejemplo()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "GymBro.slnx"))) dir = dir.Parent;
            var ruta = Path.Combine(dir?.FullName ?? throw new InvalidOperationException("No se encontró GymBro.slnx"),
                "GymBro.Api", "appsettings.Development.example.json");
            return new ConfigurationBuilder().AddJsonFile(ruta, optional: false).Build();
        }

        [Fact]
        public void No_contiene_claves_secretas()
        {
            var claves = Ejemplo().AsEnumerable().Select(kv => kv.Key).ToList();
            Assert.DoesNotContain(claves, k => k.StartsWith("ConnectionStrings", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(claves, k => k.Equals("Jwt:Key", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(claves, k => k.Contains("Password", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(claves, k => k.StartsWith("SuperAdmin", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Se_vincula_con_las_clases_de_opciones_y_coincide_con_sus_valores_por_defecto()
        {
            var cfg = Ejemplo();

            var jwt = cfg.GetSection(JwtOptions.Seccion).Get<JwtOptions>()!;
            var porDefecto = new JwtOptions();
            Assert.Equal(porDefecto.Issuer, jwt.Issuer);
            Assert.Equal(porDefecto.Audience, jwt.Audience);
            Assert.Equal(porDefecto.ExpiraMinutos, jwt.ExpiraMinutos);

            var baseline = cfg.GetSection("Baseline").Get<BaselineConfig>()!;
            Assert.Equal(new BaselineConfig().RirPorDefecto, baseline.RirPorDefecto);
            Assert.Equal(new BaselineConfig().DescansoPorDefectoSegundos, baseline.DescansoPorDefectoSegundos);

            Assert.True(cfg.GetValue<bool>(ArranqueDatos.ClaveSembrarAlIniciar));
            Assert.False(cfg.GetValue<bool>("DemoData:Habilitado"));
            Assert.Equal("http://localhost:4200", cfg["DemoData:UrlFront"]);
            Assert.Equal("http://127.0.0.1:8000", cfg["Ia:UrlBase"]);
        }
    }
}
