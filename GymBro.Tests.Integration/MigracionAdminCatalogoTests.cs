using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Infrastructure.Persistence.Seeders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace GymBro.Tests.Integration
{
    /// <summary>
    /// Etapa 9a-2b: la migración Etapa9a2b_AdminCatalogo sobre filas que ya existían (como MigracionCatalogoCoreTests).
    /// La base del contenedor se vuelve a la migración anterior (Down), se insertan filas SINTÉTICAS con la forma previa
    /// y se aplica la migración (Up). También verifica el CHECK del estado del socio y la guarda del DataSeeder.
    /// </summary>
    public class MigracionAdminCatalogoTests : IClassFixture<MigracionAdminCatalogoTests.Escenario>
    {
        private const string MigracionAnterior = "20261119011431_Etapa9a2_CatalogoGimnasio";
        private readonly Escenario _e;
        public MigracionAdminCatalogoTests(Escenario e) { _e = e; }

        public sealed record FilaEjercicio(Guid Id, bool Activo, Guid? CreadoPorUsuarioId);
        public sealed record FilaSocio(Guid Id, string Estado);
        public sealed record Columna(string ColumnName);

        [Fact]
        public void El_Down_quita_las_tres_columnas_y_el_Up_las_agrega()
        {
            Assert.DoesNotContain(_e.ColumnasTrasDown, c => c is "activo" or "creado_por_usuario_id" or "estado");
            Assert.Contains("activo", _e.ColumnasTrasUp);
            Assert.Contains("creado_por_usuario_id", _e.ColumnasTrasUp);
            Assert.Contains("estado", _e.ColumnasTrasUp);
        }

        [Fact]
        public void Las_filas_existentes_quedan_activas_sin_autor_y_el_socio_en_estado_Activo()
        {
            var ejercicio = Assert.Single(_e.Ejercicios, f => f.Id == _e.EjercicioPrevio);
            Assert.True(ejercicio.Activo);
            Assert.Null(ejercicio.CreadoPorUsuarioId);
            Assert.Equal("Activo", Assert.Single(_e.Socios, f => f.Id == _e.SocioPrevio).Estado);
        }

        [Fact]
        public async Task El_CHECK_rechaza_un_estado_de_socio_desconocido()
        {
            var ex = await Record.ExceptionAsync(() => _e.Api.ConDbAsync(async db =>
                await db.Database.ExecuteSqlRawAsync("UPDATE socios SET estado = 'Suspendido' WHERE id = {0}", _e.SocioPrevio)));
            Assert.NotNull(ex);
            Assert.Contains("CK_Socio_Estado", ex!.ToString());
        }

        [Fact]
        public async Task El_DataSeeder_no_recarga_el_dataset_si_todos_sus_ejercicios_estan_desactivados()
        {
            // Única fila de origen Dataset en esta base, desactivada; la carpeta tiene un CSV de ejercicios SINTÉTICO.
            var carpeta = Directory.CreateTempSubdirectory("gymbro-9a2b-").FullName;
            try
            {
                await File.WriteAllTextAsync(Path.Combine(carpeta, "gym_exercise_dataset.csv"),
                    "Exercise Name,Equipment,Target_Zone,Main_muscle,Synergist_Muscles,Stabilizer_Muscles,Preparation,Execution,Difficulty (1-5)\n" +
                    "Press sintetico 9a2b,Barbell,Chest,Pectoral,Triceps,Ninguno,Preparacion de prueba,Ejecucion de prueba,2\n");
                await _e.Api.ConDbAsync(async db =>
                    await db.Ejercicios.IgnoreQueryFilters().Where(x => x.Id == _e.EjercicioPrevio)
                        .ExecuteUpdateAsync(x => x.SetProperty(e => e.Activo, false)));

                await _e.Api.ConDbAsync(db => DataSeeder.SeedAsync(db, carpeta));

                var dataset = -1;
                await _e.Api.ConDbAsync(async db => dataset = await db.Ejercicios.IgnoreQueryFilters().CountAsync(x => x.Origen == OrigenEjercicio.Dataset));
                Assert.Equal(1, dataset);
            }
            finally
            {
                try { Directory.Delete(carpeta, true); } catch (IOException) { }
            }
        }

        public class Escenario : IAsyncLifetime
        {
            public GymBroApiFactory Api { get; } = new();
            public Guid EjercicioPrevio { get; } = Guid.NewGuid();
            public Guid SocioPrevio { get; } = Guid.NewGuid();
            public List<string> ColumnasTrasDown { get; private set; } = new();
            public List<string> ColumnasTrasUp { get; private set; } = new();
            public List<FilaEjercicio> Ejercicios { get; private set; } = new();
            public List<FilaSocio> Socios { get; private set; } = new();

            private const string ConsultaColumnas =
                "SELECT column_name FROM information_schema.columns " +
                "WHERE table_name IN ('ejercicios', 'socios') AND column_name IN ('activo', 'creado_por_usuario_id', 'estado')";

            public async Task InitializeAsync()
            {
                await Api.InitializeAsync(); // arranque: todas las migraciones + seeders

                await Api.ConDbAsync(async db =>
                {
                    var migrador = db.GetService<IMigrator>();
                    await migrador.MigrateAsync(MigracionAnterior); // ejecuta el Down de Etapa9a2b_AdminCatalogo
                    ColumnasTrasDown = (await db.Database.SqlQueryRaw<Columna>(ConsultaColumnas).ToListAsync()).Select(c => c.ColumnName).ToList();

                    // Filas SINTÉTICAS con la forma anterior a la etapa (sin las columnas nuevas).
                    await db.Database.ExecuteSqlRawAsync(@"
INSERT INTO ejercicios (id, nombre, equipamiento, zona_objetivo, musculo_principal, sinergistas, estabilizadores, preparacion, ejecucion,
                        dificultad, tipo_medida, origen, fecha_alta, usuario_alta)
VALUES ({0}, 'Previo sintetico', 'Barbell', 'Chest', 'Pectoral', '', '', '', '-', 1, 'Repeticiones', 'Dataset', now(), 1);", EjercicioPrevio);
                    await db.Database.ExecuteSqlRawAsync(@"
INSERT INTO socios (id, nombre, apellido, email, genero, fecha_nacimiento, objetivo, nivel, fecha_alta, usuario_alta)
VALUES ({0}, 'Previo', 'Sintetico', 'previo@migracion.test', 'F', '1995-01-01', 'Mantenimiento', 'Principiante', now(), 1);", SocioPrevio);

                    await migrador.MigrateAsync(); // vuelve a aplicar Etapa9a2b_AdminCatalogo
                    ColumnasTrasUp = (await db.Database.SqlQueryRaw<Columna>(ConsultaColumnas).ToListAsync()).Select(c => c.ColumnName).ToList();
                });

                await Api.ConDbAsync(async db =>
                {
                    Ejercicios = await db.Database.SqlQueryRaw<FilaEjercicio>(
                        "SELECT id, activo, creado_por_usuario_id FROM ejercicios").ToListAsync();
                    Socios = await db.Database.SqlQueryRaw<FilaSocio>("SELECT id, estado FROM socios").ToListAsync();
                });
            }

            public async Task DisposeAsync() => await Api.DisposeAsync();
        }
    }
}
