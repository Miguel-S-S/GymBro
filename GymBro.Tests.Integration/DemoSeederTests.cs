using GymBro.Application.Abstractions;
using GymBro.Infrastructure.Persistence.Seeders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GymBro.Tests.Integration
{
    /// <summary>
    /// Etapa 8c, T9: las cuentas demo toman su contraseña de DemoData:Password; si falta, el sembrado de demo se omite sin fallar.
    /// Se invoca el seeder directamente contra el Postgres descartable (en el arranque solo corre en Development).
    /// La contraseña usada acá es un valor de prueba generado en el momento.
    /// </summary>
    public class DemoSeederTests : IClassFixture<GymBroApiFactory>
    {
        private readonly GymBroApiFactory _api;
        public DemoSeederTests(GymBroApiFactory api) { _api = api; }

        [Fact]
        public async Task Sin_DemoData_Password_se_omite_y_con_ella_las_cuentas_la_usan()
        {
            var hasheador = _api.Services.GetRequiredService<IHasheadorContrasena>();

            // 1. Falta la clave (null o en blanco): no falla y no crea nada.
            foreach (var faltante in new[] { null, "", "   " })
            {
                var creado = false;
                await _api.ConDbAsync(async db => creado = await DemoSeeder.SeedAsync(db, hasheador, "http://localhost:4200", faltante));
                Assert.False(creado);
            }
            await _api.ConDbAsync(async db =>
                Assert.False(await db.Gimnasios.AnyAsync(g => g.Codigo == DemoSeeder.CodigoGimnasio)));

            // 2. Con la clave: crea los datos y todas las cuentas demo verifican con esa contraseña.
            var password = $"prueba-{Guid.NewGuid():N}";
            var sembrado = false;
            await _api.ConDbAsync(async db => sembrado = await DemoSeeder.SeedAsync(db, hasheador, "http://localhost:4200", password));
            Assert.True(sembrado);

            await _api.ConDbAsync(async db =>
            {
                var gimnasio = await db.Gimnasios.SingleAsync(g => g.Codigo == DemoSeeder.CodigoGimnasio);
                var cuentas = await db.Usuarios
                    .Where(u => u.GimnasioId == gimnasio.Id || (u.Socio != null && (u.Socio.Email.EndsWith("@test.local"))))
                    .ToListAsync();
                Assert.Equal(5, cuentas.Count); // dueño, trainer y 3 socios con cuenta
                Assert.All(cuentas, u => Assert.True(hasheador.Verificar(u.PasswordHash, password)));
            });
        }
    }
}
