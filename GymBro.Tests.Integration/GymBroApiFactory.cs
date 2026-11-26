using System.Net.Http.Headers;
using GymBro.Application.Abstractions;
using GymBro.Domain.Entities;
using GymBro.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace GymBro.Tests.Integration
{
    /// <summary>
    /// Levanta GymBro.Api en memoria contra un PostgreSQL + pgvector descartable (Testcontainers), con la misma imagen
    /// que gymbro-db pero en un contenedor aparte: no toca la base local. Requiere Docker en ejecución.
    /// Las migraciones reales se aplican al arrancar (Program.cs); el seeding de CSV queda vacío porque
    /// Datasets:Ruta apunta a una carpeta sin archivos.
    /// </summary>
    public class GymBroApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        // Clave solo para pruebas (no es un secreto real): firma los JWT de la API levantada en el test.
        private const string ClaveJwtDePrueba = "clave-de-prueba-gymbro-integracion-0123456789";

        /// <summary>Etapa 11: clave del QR de asistencia de las pruebas (no es un secreto real).</summary>
        public const string ClaveQrDePrueba = "clave-qr-de-prueba-gymbro-integracion-0123";

        private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("pgvector/pgvector:pg16").Build();

        private readonly string _datasetsVacios = Directory.CreateTempSubdirectory("gymbro-tests-").FullName;

        /// <summary>Etapa 10b-1: cadena del PostgreSQL descartable con el usuario administrador (para probar scripts y roles).</summary>
        public string CadenaConexionAdmin => _db.GetConnectionString();

        /// <summary>Carpeta de datasets de la API levantada (por defecto, vacía). Las subclases pueden copiar CSV acá.</summary>
        protected string CarpetaDatasets => _datasetsVacios;

        /// <summary>Entorno ASP.NET de la API levantada. "Testing": sin Swagger ni DemoSeeder (solo se activan en Development).</summary>
        protected virtual string Entorno => "Testing";

        /// <summary>Configuración adicional para subclases (se aplica después de la base).</summary>
        protected virtual void ConfigurarExtra(IWebHostBuilder builder) { }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(Entorno);
            builder.UseSetting("ConnectionStrings:DefaultConnection", _db.GetConnectionString());
            builder.UseSetting("Jwt:Key", ClaveJwtDePrueba);
            builder.UseSetting("Datasets:Ruta", _datasetsVacios);
            builder.UseSetting("SuperAdmin:Email", "");
            builder.UseSetting("SuperAdmin:Password", "");
            // Etapa 11: clave solo para pruebas (no es un secreto real) del QR de asistencia.
            builder.UseSetting("Asistencia:ClaveQr", ClaveQrDePrueba);
            ConfigurarExtra(builder);
        }

        public virtual async Task InitializeAsync()
        {
            await _db.StartAsync();
            _ = Server; // fuerza el arranque (migraciones) antes de sembrar datos
        }

        public new async Task DisposeAsync()
        {
            await base.DisposeAsync();
            await _db.DisposeAsync();
            try { Directory.Delete(_datasetsVacios, true); } catch (IOException) { }
        }

        /// <summary>Ejecuta código con el DbContext fuera de un request (proceso de sistema: sin filtros de tenant).</summary>
        public async Task ConDbAsync(Func<GymBroDbContext, Task> accion)
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<GymBroDbContext>();
            await accion(db);
            await db.SaveChangesAsync();
        }

        /// <summary>Cliente HTTP autenticado con el JWT que emite la propia API para ese usuario.</summary>
        public HttpClient ClienteComo(Usuario usuario)
        {
            using var scope = Services.CreateScope();
            var token = scope.ServiceProvider.GetRequiredService<IGeneradorToken>().Generar(usuario).Token;
            var cliente = CreateClient();
            cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return cliente;
        }
    }
}
