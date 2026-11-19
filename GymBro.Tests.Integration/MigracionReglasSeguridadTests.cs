using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace GymBro.Tests.Integration
{
    /// <summary>
    /// Etapa 10a: la migración Etapa10a_ReglasSeguridad es reversible (Down y Up sobre una base con una revisión ya
    /// existente) y la base exige los CHECK de los enums y la clave única de idempotencia. Datos SINTÉTICOS.
    /// </summary>
    public class MigracionReglasSeguridadTests : IClassFixture<MigracionReglasSeguridadTests.Escenario>
    {
        private const string MigracionAnterior = "20261119122415_Etapa9a2b_AdminCatalogo";
        private readonly Escenario _e;
        public MigracionReglasSeguridadTests(Escenario e) { _e = e; }

        public sealed record Conteo(int Cantidad);

        [Fact]
        public void El_Down_quita_la_tabla_y_la_columna_y_el_Up_las_vuelve_a_crear_sin_tocar_la_revision_existente()
        {
            Assert.Equal((0, 0), (_e.TablaTrasDown, _e.ColumnaTrasDown));
            Assert.Equal((1, 1), (_e.TablaTrasUp, _e.ColumnaTrasUp));
            Assert.Equal(1, _e.RevisionesTrasUp);
        }

        [Theory]
        [InlineData("regla_codigo", "'R-OTRA'", "CK_AlertaSeguridad_ReglaCodigo")]
        [InlineData("tipo", "'Otro'", "CK_AlertaSeguridad_Tipo")]
        [InlineData("severidad", "'Critica'", "CK_AlertaSeguridad_Severidad")]
        [InlineData("estado", "'2'", "CK_AlertaSeguridad_Estado")]
        public async Task Los_CHECK_rechazan_valores_fuera_de_los_enums(string columna, string valor, string restriccion)
        {
            // La columna y el valor son literales fijos de la prueba (InlineData); el id va como parámetro.
            var sql = "UPDATE alertas_seguridad SET " + columna + " = " + valor + " WHERE id = {0}";
            var ex = await Record.ExceptionAsync(() => _e.Api.ConDbAsync(async db => await db.Database.ExecuteSqlRawAsync(sql, _e.AlertaId)));
            Assert.NotNull(ex);
            Assert.Contains(restriccion, ex!.ToString());
        }

        [Fact]
        public async Task La_clave_de_idempotencia_es_unica_aunque_la_fila_este_dada_de_baja()
        {
            var ex = await Record.ExceptionAsync(() => _e.Api.ConDbAsync(db =>
            {
                db.AlertasSeguridad.Add(Alerta(_e.SocioId, fechaBaja: null));
                return Task.CompletedTask;
            }));
            Assert.IsType<DbUpdateException>(ex);
        }

        private static AlertaSeguridad Alerta(Guid socioId, DateTime? fechaBaja) => new()
        {
            SocioId = socioId, ReglaCodigo = CodigosRegla.FcReposo, ClaveOrigen = "clave-sintetica", Tipo = TipoAlerta.Alerta,
            Severidad = SeveridadAlerta.Advertencia, Mensaje = "Sintetica", Detalle = "{}", FechaBaja = fechaBaja,
        };

        public class Escenario : IAsyncLifetime
        {
            public GymBroApiFactory Api { get; } = new();
            public Guid SocioId { get; } = Guid.NewGuid();
            public Guid AlertaId { get; private set; }
            public int TablaTrasDown, ColumnaTrasDown, TablaTrasUp, ColumnaTrasUp, RevisionesTrasUp;

            private const string Tabla = "SELECT count(*)::int AS cantidad FROM information_schema.tables WHERE table_name = 'alertas_seguridad'";
            private const string Columna = "SELECT count(*)::int AS cantidad FROM information_schema.columns WHERE table_name = 'revisiones_prescripcion' AND column_name = 'violaciones_limites'";

            public async Task InitializeAsync()
            {
                await Api.InitializeAsync();
                var revisor = Guid.NewGuid();
                await Api.ConDbAsync(async db =>
                {
                    var migrador = db.GetService<IMigrator>();
                    await migrador.MigrateAsync(MigracionAnterior);   // Down de Etapa10a_ReglasSeguridad
                    TablaTrasDown = (await db.Database.SqlQueryRaw<Conteo>(Tabla).SingleAsync()).Cantidad;
                    ColumnaTrasDown = (await db.Database.SqlQueryRaw<Conteo>(Columna).SingleAsync()).Cantidad;

                    // Datos con la forma anterior: un socio, una rutina y una revisión (sin la columna nueva).
                    await db.Database.ExecuteSqlRawAsync(@"
INSERT INTO socios (id, nombre, apellido, email, genero, fecha_nacimiento, objetivo, nivel, estado, fecha_alta, usuario_alta)
VALUES ({0}, 'Previo', 'Sintetico', 'previo@reglas.migracion', 'F', '1995-01-01', 'Mantenimiento', 'Principiante', 'Activo', now(), 1);", SocioId);
                    var rutina = Guid.NewGuid();
                    await db.Database.ExecuteSqlRawAsync(@"
INSERT INTO prescripciones_entrenamiento (id, socio_id, origen_generacion, estado, fecha_alta, usuario_alta)
VALUES ({0}, {1}, 'PRUEBA', 'Aprobada', now(), 1);", rutina, SocioId);
                    await db.Database.ExecuteSqlRawAsync(@"
INSERT INTO revisiones_prescripcion (id, prescripcion_entrenamiento_id, revisor_usuario_id, decision, fecha)
VALUES ({0}, {1}, {2}, 'Aprobada', now());", Guid.NewGuid(), rutina, revisor);

                    await migrador.MigrateAsync();                    // Up de Etapa10a_ReglasSeguridad
                    TablaTrasUp = (await db.Database.SqlQueryRaw<Conteo>(Tabla).SingleAsync()).Cantidad;
                    ColumnaTrasUp = (await db.Database.SqlQueryRaw<Conteo>(Columna).SingleAsync()).Cantidad;
                    RevisionesTrasUp = (await db.Database.SqlQueryRaw<Conteo>(
                        "SELECT count(*)::int AS cantidad FROM revisiones_prescripcion WHERE violaciones_limites IS NULL").SingleAsync()).Cantidad;
                });

                // Una alerta dada de baja con la clave que después se intenta repetir.
                var alerta = Alerta(SocioId, DateTime.UtcNow);
                AlertaId = alerta.Id;
                await Api.ConDbAsync(db => { db.AlertasSeguridad.Add(alerta); return Task.CompletedTask; });
            }

            public async Task DisposeAsync() => await Api.DisposeAsync();
        }
    }
}
