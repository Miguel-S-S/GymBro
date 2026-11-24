using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Infrastructure.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace GymBro.Tests.Integration
{
    /// <summary>
    /// Etapa 7d: la migración Etapa7d_ProtocoloPiernas es aditiva y reversible. Sobre una base con datos de la forma
    /// anterior (socio, rutina, sesión con un ejercicio) se verifica: Down quita tabla y columnas; Up sin los ejercicios
    /// curados siembra 0 filas; Up con los 10 ejercicios siembra 10; los datos previos quedan intactos y con los valores
    /// por defecto; la base exige los CHECK y el índice único. Datos SINTÉTICOS (los UUID de la siembra se insertan como
    /// ejercicios de prueba en el contenedor descartable).
    /// </summary>
    public class MigracionProtocoloPiernasTests : IClassFixture<MigracionProtocoloPiernasTests.Escenario>
    {
        private const string MigracionAnterior = "20261119181539_Etapa10a_ReglasSeguridad";
        private readonly Escenario _e;
        public MigracionProtocoloPiernasTests(Escenario e) { _e = e; }

        public sealed record Conteo(int Cantidad);
        public sealed record Texto(string Valor);

        [Fact]
        public void El_Down_quita_la_tabla_y_las_columnas_y_el_Up_las_vuelve_a_crear()
        {
            Assert.Equal((0, 0), (_e.TablaTrasDown, _e.ColumnasTrasDown));
            Assert.Equal((1, Escenario.ColumnasNuevas.Length), (_e.TablaTrasUp, _e.ColumnasTrasUp));
        }

        [Fact]
        public void Sin_los_ejercicios_curados_la_siembra_inserta_0_filas_y_con_ellos_10()
        {
            Assert.Equal(0, _e.SiembraSinEjercicios);
            Assert.Equal(10, _e.SiembraConEjercicios);
            Assert.Equal(10, Etapa7d_ProtocoloPiernas.Siembra.Length);
        }

        [Fact]
        public async Task La_siembra_guarda_grupo_rol_clase_exclusiones_y_orden_de_la_especificacion()
        {
            var filas = new List<EjercicioProtocolo>();
            await _e.Api.ConDbAsync(async db => filas = await db.EjerciciosProtocolo.ToListAsync());
            var prensa = filas.Single(f => f.EjercicioId == Guid.Parse("69e15aaf-ca0d-433b-be03-6b496450b1d2"));
            Assert.Equal((GrupoMuscularPiernas.Cuadriceps, RolEjercicioProtocolo.Referencia, ClaseEjercicio.Multiarticular,
                    (NivelLimitacion?)NivelLimitacion.Alta, (NivelLimitacion?)NivelLimitacion.Alta, (short)1, "PIERNAS", "piernas-1.0"),
                (prensa.Grupo, prensa.Rol, prensa.Clase, prensa.RodillaExcluirDesde, prensa.LumbarExcluirDesde, prensa.OrdenRotacion,
                    prensa.ProtocoloCodigo, prensa.ProtocoloVersion));
            var curl = filas.Single(f => f.EjercicioId == Guid.Parse("14a284c5-5159-4bdb-83f7-733154e07228"));
            Assert.Equal((GrupoMuscularPiernas.Isquiotibiales, RolEjercicioProtocolo.Referencia, (NivelLimitacion?)null, (NivelLimitacion?)null),
                (curl.Grupo, curl.Rol, curl.RodillaExcluirDesde, curl.LumbarExcluirDesde));
            Assert.Equal(new[] { 5, 3, 2 }, new[] { GrupoMuscularPiernas.Cuadriceps, GrupoMuscularPiernas.Isquiotibiales, GrupoMuscularPiernas.Pantorrillas }
                .Select(g => filas.Count(f => f.Grupo == g)));
            Assert.Equal(3, filas.Count(f => f.Rol == RolEjercicioProtocolo.Referencia));
        }

        [Fact]
        public void Los_datos_previos_quedan_intactos_con_los_valores_por_defecto()
        {
            Assert.Equal("SinLimitacion/SinLimitacion/DatasetExterno", _e.SocioPrevio);
            Assert.Equal("DatasetExterno/false/false/null/null", _e.RutinaPrevia);
            Assert.Equal("sin vinculo", _e.SesionPrevia);
        }

        [Theory]
        [InlineData("UPDATE socios SET limitacion_rodilla = 'Leve' WHERE id = {0}", "CK_Socio_LimitacionRodilla")]
        [InlineData("UPDATE socios SET limitacion_lumbar = '2' WHERE id = {0}", "CK_Socio_LimitacionLumbar")]
        [InlineData("UPDATE socios SET origen_datos = 'SinClasificar' WHERE id = {0}", "CK_Socio_OrigenDatos")]
        [InlineData("UPDATE prescripciones_entrenamiento SET origen_datos = 'Otro' WHERE socio_id = {0}", "CK_PrescripcionEntrenamiento_OrigenDatos")]
        [InlineData("UPDATE prescripciones_entrenamiento SET numero_generacion = 0 WHERE socio_id = {0}", "CK_PrescripcionEntrenamiento_NumeroGeneracion")]
        [InlineData("UPDATE prescripciones_ejercicios SET tempo_excentrico_seg = 11 WHERE prescripcion_entrenamiento_id IN (SELECT id FROM prescripciones_entrenamiento WHERE socio_id = {0})", "CK_PrescripcionEjercicio_TempoExcentricoSeg")]
        [InlineData("UPDATE prescripciones_ejercicios SET carga_sugerida_kg = -1 WHERE prescripcion_entrenamiento_id IN (SELECT id FROM prescripciones_entrenamiento WHERE socio_id = {0})", "CK_PrescripcionEjercicio_CargaSugeridaKg")]
        [InlineData("UPDATE prescripciones_ejercicios SET rol = 'Fija' WHERE prescripcion_entrenamiento_id IN (SELECT id FROM prescripciones_entrenamiento WHERE socio_id = {0})", "CK_PrescripcionEjercicio_Rol")]
        [InlineData("UPDATE prescripciones_ejercicios SET grupo = 'Gluteos' WHERE prescripcion_entrenamiento_id IN (SELECT id FROM prescripciones_entrenamiento WHERE socio_id = {0})", "CK_PrescripcionEjercicio_Grupo")]
        [InlineData("UPDATE ejercicios_protocolo SET rodilla_excluir_desde = 'Leve' WHERE {0}::uuid IS NOT NULL", "CK_EjercicioProtocolo_RodillaExcluirDesde")]
        [InlineData("UPDATE ejercicios_protocolo SET clase = 'Otra' WHERE {0}::uuid IS NOT NULL", "CK_EjercicioProtocolo_Clase")]
        public async Task Los_CHECK_rechazan_valores_fuera_de_rango(string sql, string restriccion)
        {
            var ex = await Assert.ThrowsAsync<PostgresException>(() =>
                _e.Api.ConDbAsync(db => db.Database.ExecuteSqlRawAsync(sql, _e.SocioId)));
            Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
            Assert.Equal(restriccion, ex.ConstraintName);
        }

        [Fact]
        public async Task El_numero_de_generacion_es_unico_por_socio_y_protocolo_entre_las_filas_activas()
        {
            static string Insertar(string fechaBaja) => @"INSERT INTO prescripciones_entrenamiento (id, socio_id, origen_generacion, estado,
protocolo_codigo, protocolo_version, numero_generacion, origen_datos, excluida_estudio, es_modificada, fecha_alta, usuario_alta, fecha_baja)
VALUES ({0}, {1}, 'PRUEBA', 'Rechazada', 'PIERNAS', 'piernas-1.0', 7, 'Sintetico', false, false, now(), 1, " + fechaBaja + ");";
            await _e.Api.ConDbAsync(db => db.Database.ExecuteSqlRawAsync(Insertar("NULL"), Guid.NewGuid(), _e.SocioId));
            var ex = await Assert.ThrowsAsync<PostgresException>(() =>
                _e.Api.ConDbAsync(db => db.Database.ExecuteSqlRawAsync(Insertar("NULL"), Guid.NewGuid(), _e.SocioId)));
            Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);
            // Una fila dada de baja no ocupa el número (índice parcial).
            await _e.Api.ConDbAsync(db => db.Database.ExecuteSqlRawAsync(Insertar("now()"), Guid.NewGuid(), _e.SocioId));
        }

        public class Escenario : IAsyncLifetime
        {
            public GymBroApiFactory Api { get; } = new();
            public Guid SocioId { get; } = Guid.NewGuid();
            public int TablaTrasDown, ColumnasTrasDown, TablaTrasUp, ColumnasTrasUp, SiembraSinEjercicios, SiembraConEjercicios;
            public string SocioPrevio = "", RutinaPrevia = "", SesionPrevia = "";

            public static readonly string[] ColumnasNuevas =
            {
                "socios.limitacion_rodilla", "socios.limitacion_lumbar", "socios.origen_datos",
                "prescripciones_entrenamiento.protocolo_codigo", "prescripciones_entrenamiento.protocolo_version",
                "prescripciones_entrenamiento.numero_generacion", "prescripciones_entrenamiento.origen_datos",
                "prescripciones_entrenamiento.excluida_estudio", "prescripciones_entrenamiento.es_modificada",
                "prescripciones_entrenamiento.entrada_snapshot", "prescripciones_ejercicios.rol", "prescripciones_ejercicios.grupo",
                "prescripciones_ejercicios.tempo_excentrico_seg", "prescripciones_ejercicios.carga_sugerida_kg",
                "prescripciones_ejercicios.motivo_seleccion", "sesiones_ejercicios.prescripcion_ejercicio_id",
            };

            private const string Tabla = "SELECT count(*)::int AS cantidad FROM information_schema.tables WHERE table_name = 'ejercicios_protocolo'";
            private static readonly string Columnas = "SELECT count(*)::int AS cantidad FROM information_schema.columns WHERE table_name || '.' || column_name IN ("
                + string.Join(", ", ColumnasNuevas.Select(c => $"'{c}'")) + ")";
            private const string Sembradas = "SELECT count(*)::int AS cantidad FROM ejercicios_protocolo";

            public async Task InitializeAsync()
            {
                await Api.InitializeAsync();
                await Api.ConDbAsync(async db =>
                {
                    var migrador = db.GetService<IMigrator>();
                    await migrador.MigrateAsync(MigracionAnterior);   // Down de Etapa7d_ProtocoloPiernas
                    TablaTrasDown = (await db.Database.SqlQueryRaw<Conteo>(Tabla).SingleAsync()).Cantidad;
                    ColumnasTrasDown = (await db.Database.SqlQueryRaw<Conteo>(Columnas).SingleAsync()).Cantidad;

                    // Datos con la forma anterior: socio, ejercicio, rutina con una línea y una sesión de esa rutina.
                    var ejercicio = Guid.NewGuid();
                    var rutina = Guid.NewGuid();
                    var linea = Guid.NewGuid();
                    var sesion = Guid.NewGuid();
                    await db.Database.ExecuteSqlRawAsync(@"
INSERT INTO socios (id, nombre, apellido, email, genero, fecha_nacimiento, objetivo, nivel, estado, fecha_alta, usuario_alta)
VALUES ({0}, 'Previo', 'Sintetico', 'previo@piernas.migracion', 'F', '1995-01-01', 'Mantenimiento', 'Principiante', 'Activo', now(), 1);
INSERT INTO ejercicios (id, nombre, equipamiento, zona_objetivo, musculo_principal, sinergistas, estabilizadores, preparacion, ejecucion,
    dificultad, tipo_medida, origen, activo, fecha_alta, usuario_alta)
VALUES ({1}, 'Previo sintetico', '', 'Legs', '', '', '', '', '', 1, 'Repeticiones', 'Dataset', true, now(), 1);
INSERT INTO prescripciones_entrenamiento (id, socio_id, origen_generacion, estado, fecha_alta, usuario_alta)
VALUES ({2}, {0}, 'PRUEBA', 'Aprobada', now(), 1);
INSERT INTO prescripciones_ejercicios (id, prescripcion_entrenamiento_id, ejercicio_id, orden, series_objetivo, repeticiones_minimas,
    repeticiones_maximas, rir_objetivo, descanso_objetivo_segundos, fecha_alta, usuario_alta)
VALUES ({3}, {2}, {1}, 1, 3, 8, 12, 2, 90, now(), 1);
INSERT INTO sesiones (id, socio_id, inicio, calorias_quemadas, frecuencia_cardiaca_maxima, frecuencia_cardiaca_promedio,
    prescripcion_entrenamiento_id, fecha_alta, usuario_alta)
VALUES ({4}, {0}, now(), 0, 0, 0, {2}, now(), 1);
INSERT INTO sesiones_ejercicios (id, sesion_id, ejercicio_id, orden, fecha_alta, usuario_alta)
VALUES ({5}, {4}, {1}, 1, now(), 1);", SocioId, ejercicio, rutina, linea, sesion, Guid.NewGuid());

                    await migrador.MigrateAsync();                    // Up sin los ejercicios curados: 0 filas
                    SiembraSinEjercicios = (await db.Database.SqlQueryRaw<Conteo>(Sembradas).SingleAsync()).Cantidad;

                    await migrador.MigrateAsync(MigracionAnterior);   // Down otra vez y alta de los 10 ejercicios con sus UUID
                    foreach (var s in Etapa7d_ProtocoloPiernas.Siembra)
                        await db.Database.ExecuteSqlRawAsync(@"
INSERT INTO ejercicios (id, nombre, equipamiento, zona_objetivo, musculo_principal, sinergistas, estabilizadores, preparacion, ejecucion,
    dificultad, tipo_medida, origen, activo, fecha_alta, usuario_alta)
VALUES ({0}, 'Curado sintetico', '', 'Legs', '', '', '', '', '', 1, 'Repeticiones', 'Proyecto', true, now(), 1);", s.EjercicioId);

                    await migrador.MigrateAsync();                    // Up con los 10: 10 filas
                    SiembraConEjercicios = (await db.Database.SqlQueryRaw<Conteo>(Sembradas).SingleAsync()).Cantidad;
                    TablaTrasUp = (await db.Database.SqlQueryRaw<Conteo>(Tabla).SingleAsync()).Cantidad;
                    ColumnasTrasUp = (await db.Database.SqlQueryRaw<Conteo>(Columnas).SingleAsync()).Cantidad;

                    SocioPrevio = (await db.Database.SqlQueryRaw<Texto>(
                        "SELECT limitacion_rodilla || '/' || limitacion_lumbar || '/' || origen_datos AS valor FROM socios WHERE id = {0}", SocioId).SingleAsync()).Valor;
                    RutinaPrevia = (await db.Database.SqlQueryRaw<Texto>(
                        "SELECT origen_datos || '/' || excluida_estudio || '/' || es_modificada || '/' || coalesce(protocolo_codigo, 'null') || '/' || coalesce(numero_generacion::text, 'null') AS valor FROM prescripciones_entrenamiento WHERE id = {0}", rutina).SingleAsync()).Valor;
                    SesionPrevia = (await db.Database.SqlQueryRaw<Texto>(
                        "SELECT CASE WHEN prescripcion_ejercicio_id IS NULL THEN 'sin vinculo' ELSE 'con vinculo' END AS valor FROM sesiones_ejercicios WHERE sesion_id = {0}", sesion).SingleAsync()).Valor;
                });
            }

            public async Task DisposeAsync() => await Api.DisposeAsync();
        }
    }
}
