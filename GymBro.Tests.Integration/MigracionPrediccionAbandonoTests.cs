using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace GymBro.Tests.Integration
{
    /// <summary>Etapa 10b-1: la migracion Etapa10b1_PrediccionAbandono es reversible y la base exige el rango del puntaje.</summary>
    public class MigracionPrediccionAbandonoTests
    {
        [Fact]
        public async Task Down_quita_la_tabla_Up_la_crea_y_el_CHECK_rechaza_puntajes_fuera_de_0_a_1()
        {
            const string tabla = "SELECT count(*)::int AS cantidad FROM information_schema.tables WHERE table_name = 'prediccion_abandono'";
            int trasDown = -1, trasUp = -1;
            Exception? fueraDeRango = null;
            var api = new GymBroApiFactory();
            await api.InitializeAsync();
            try
            {
                await api.ConDbAsync(async db =>
                {
                    var migrador = db.GetService<IMigrator>();
                    await migrador.MigrateAsync("20261125210310_Etapa11_AsistenciaYCuotas");
                    trasDown = (await db.Database.SqlQueryRaw<MigracionReglasSeguridadTests.Conteo>(tabla).SingleAsync()).Cantidad;
                    await migrador.MigrateAsync();
                    trasUp = (await db.Database.SqlQueryRaw<MigracionReglasSeguridadTests.Conteo>(tabla).SingleAsync()).Cantidad;
                });
                await using var con = new NpgsqlConnection(api.CadenaConexionAdmin);
                await con.OpenAsync();
                var socio = Guid.NewGuid();
                await new NpgsqlCommand($@"INSERT INTO socios (id, nombre, apellido, email, genero, fecha_nacimiento, objetivo, nivel, estado,
                    limitacion_rodilla, limitacion_lumbar, origen_datos, fecha_alta, usuario_alta)
                    VALUES ('{socio}', 'P', 'S', 'p@migracion10b1.test', 'F', '1990-01-01', 'Mantenimiento', 'Principiante', 'Activo',
                    'SinLimitacion', 'SinLimitacion', 'Sintetico', now(), 1)", con).ExecuteNonQueryAsync();
                fueraDeRango = await Record.ExceptionAsync(() => new NpgsqlCommand($@"INSERT INTO prediccion_abandono
                    (id, socio_id, fecha, puntaje, version_modelo, origen_datos, fecha_alta, usuario_alta)
                    VALUES ('{Guid.NewGuid()}', '{socio}', DATE '2026-01-05', 1.5, 'v', 'Sintetico', now(), 1)", con).ExecuteNonQueryAsync());
            }
            finally
            {
                await api.DisposeAsync();
            }
            Assert.Equal((0, 1), (trasDown, trasUp));
            Assert.Contains("CK_PrediccionAbandono_Puntaje", fueraDeRango!.Message);
        }
    }
}
