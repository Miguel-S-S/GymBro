using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace GymBro.Tests.Integration
{
    /// <summary>
    /// Etapa 10b-1: docs/pfc/scripts/rol-analitica.sql contra PostgreSQL descartable. Escenario SINTETICO: un gimnasio con
    /// un socio Sintetico y uno Real (con asistencia, cuota y bienestar cada uno) y otro gimnasio solo con un socio Real.
    /// Se verifica que las vistas exponen solo lo Sintetico, que los roles no leen las tablas de la aplicacion y que el rol
    /// de puntuacion solo inserta puntajes.
    /// </summary>
    public class RolAnaliticaTests : IClassFixture<RolAnaliticaTests.Escenario>
    {
        private const string ClaveLectura = "clave-lectura-de-prueba-0123";
        private const string ClavePuntuacion = "clave-puntuacion-de-prueba-0123";
        private readonly Escenario _e;
        public RolAnaliticaTests(Escenario e) { _e = e; }

        private static async Task<long> ContarAsync(NpgsqlConnection con, string sql)
        {
            await using var cmd = new NpgsqlCommand(sql, con);
            return Convert.ToInt64(await cmd.ExecuteScalarAsync());
        }

        private async Task<NpgsqlConnection> ComoAsync(string usuario, string clave)
        {
            var b = new NpgsqlConnectionStringBuilder(_e.CadenaAdmin) { Username = usuario, Password = clave, Pooling = false };
            var con = new NpgsqlConnection(b.ConnectionString);
            await con.OpenAsync();
            return con;
        }

        [Fact]
        public async Task Las_vistas_exponen_solo_socios_Sintetico()
        {
            await using var con = await ComoAsync("gymbro_analitica_lectura", ClaveLectura);
            Assert.Equal(1, await ContarAsync(con, "SELECT count(*) FROM analitica.v_socios"));
            Assert.Equal(1, await ContarAsync(con, "SELECT count(*) FROM analitica.v_asistencias"));
            Assert.Equal(1, await ContarAsync(con, "SELECT count(*) FROM analitica.v_cuotas"));
            Assert.Equal(1, await ContarAsync(con, "SELECT count(*) FROM analitica.v_bienestar"));
            Assert.Equal(1, await ContarAsync(con, "SELECT count(*) FROM analitica.v_series"));
            Assert.Equal(1, await ContarAsync(con, "SELECT count(*) FROM analitica.v_gimnasios"));
            Assert.Equal(0, await ContarAsync(con, $"SELECT count(*) FROM analitica.v_socios WHERE socio_id IN ('{_e.Real1}', '{_e.Real2}')"));
            // La vista de socios no expone nombre, email ni DNI.
            Assert.Equal(0, await ContarAsync(con, "SELECT count(*) FROM information_schema.columns WHERE table_schema = 'analitica' AND column_name IN ('nombre', 'apellido', 'email', 'dni')"
                + " AND table_name = 'v_socios'"));
        }

        [Theory]
        [InlineData("SELECT count(*) FROM socios")]
        [InlineData("SELECT count(*) FROM registros_bienestar_diario")]
        [InlineData("SELECT count(*) FROM prediccion_abandono")]
        [InlineData("SELECT count(*) FROM usuarios")]
        public async Task Los_roles_no_leen_las_tablas_de_la_aplicacion(string sql)
        {
            foreach (var (usuario, clave) in new[] { ("gymbro_analitica_lectura", ClaveLectura), ("gymbro_analitica_puntuacion", ClavePuntuacion) })
            {
                await using var con = await ComoAsync(usuario, clave);
                var ex = await Assert.ThrowsAsync<PostgresException>(() => ContarAsync(con, sql));
                Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, ex.SqlState);
            }
        }

        [Fact]
        public async Task La_lectura_no_escribe_y_la_puntuacion_solo_inserta_puntajes()
        {
            var insertar = $"INSERT INTO prediccion_abandono (id, socio_id, fecha, puntaje, version_modelo, origen_datos, fecha_alta, usuario_alta) " +
                           $"VALUES ('{Guid.NewGuid()}', '{_e.Sintetico}', DATE '2026-03-02', 0.25, 'prueba', 'Sintetico', now(), 1) ON CONFLICT DO NOTHING";
            await using (var lectura = await ComoAsync("gymbro_analitica_lectura", ClaveLectura))
            {
                var ex = await Assert.ThrowsAsync<PostgresException>(() => new NpgsqlCommand(insertar, lectura).ExecuteNonQueryAsync());
                Assert.Contains(ex.SqlState, new[] { PostgresErrorCodes.InsufficientPrivilege, PostgresErrorCodes.ReadOnlySqlTransaction });
            }
            await using (var puntuacion = await ComoAsync("gymbro_analitica_puntuacion", ClavePuntuacion))
            {
                Assert.Equal(1, await new NpgsqlCommand(insertar, puntuacion).ExecuteNonQueryAsync());
                var ex = await Assert.ThrowsAsync<PostgresException>(() =>
                    new NpgsqlCommand($"UPDATE prediccion_abandono SET puntaje = 1 WHERE socio_id = '{_e.Sintetico}'", puntuacion).ExecuteNonQueryAsync());
                Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, ex.SqlState);
                Assert.Equal(1, await ContarAsync(puntuacion, "SELECT count(*) FROM analitica.v_prediccion_abandono"));
            }
        }

        [Fact]
        public async Task Sin_completar_las_contrasenas_el_script_falla_y_es_idempotente_al_repetirlo()
        {
            var ex = await Record.ExceptionAsync(() => _e.EjecutarComoAdminAsync(Escenario.Script()));
            Assert.Contains("Complete las dos contrasenas", ex!.Message);
            await _e.EjecutarComoAdminAsync(Escenario.Script(ClaveLectura, ClavePuntuacion));
            await using var con = await ComoAsync("gymbro_analitica_lectura", ClaveLectura);
            Assert.Equal(1, await ContarAsync(con, "SELECT count(*) FROM analitica.v_socios"));
        }

        public class Escenario : IAsyncLifetime
        {
            public GymBroApiFactory Api { get; } = new();
            public Guid Sintetico { get; } = Guid.NewGuid();
            public Guid Real1 { get; } = Guid.NewGuid();
            public Guid Real2 { get; } = Guid.NewGuid();
            public string CadenaAdmin { get; private set; } = string.Empty;

            public static string Script(string? lectura = null, string? puntuacion = null)
            {
                var dir = new DirectoryInfo(AppContext.BaseDirectory);
                while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "GymBro.slnx"))) dir = dir.Parent;
                var sql = File.ReadAllText(Path.Combine(dir!.FullName, "docs", "pfc", "scripts", "rol-analitica.sql"));
                if (lectura is not null) sql = sql.Replace("'REEMPLAZAR_CLAVE_LECTURA'", $"'{lectura}'");
                if (puntuacion is not null) sql = sql.Replace("'REEMPLAZAR_CLAVE_PUNTUACION'", $"'{puntuacion}'");
                return sql;
            }

            public async Task EjecutarComoAdminAsync(string sql)
            {
                await using var con = new NpgsqlConnection(CadenaAdmin);
                await con.OpenAsync();
                await using var cmd = new NpgsqlCommand(sql, con);
                await cmd.ExecuteNonQueryAsync();
            }

            public async Task InitializeAsync()
            {
                await Api.InitializeAsync();
                CadenaAdmin = Api.CadenaConexionAdmin;
                var g1 = new Gimnasio { Nombre = "Analitica 1", Codigo = "ANAL-1" };
                var g2 = new Gimnasio { Nombre = "Analitica 2", Codigo = "ANAL-2" };
                Socio S(Guid id, Guid g, OrigenDatos o) => new()
                {
                    Id = id, Nombre = "N", Apellido = "A", Email = $"{id}@analitica.test", Genero = "F", Dni = null,
                    FechaNacimiento = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc), GimnasioId = g,
                    Objetivo = ObjetivoEntrenamiento.Mantenimiento, Nivel = NivelExperiencia.Intermedio, OrigenDatos = o,
                };
                var dueno = new Usuario { Email = "dueno@analitica.test", NombreCompleto = "D", Rol = RolUsuario.AdministradorGimnasio, GimnasioId = g1.Id, PasswordHash = "-" };
                await Api.ConDbAsync(db =>
                {

                    db.Gimnasios.AddRange(g1, g2);
                    db.Usuarios.Add(dueno);
                    db.Socios.AddRange(S(Sintetico, g1.Id, OrigenDatos.Sintetico), S(Real1, g1.Id, OrigenDatos.Real), S(Real2, g2.Id, OrigenDatos.Real));
                    foreach (var (socio, g) in new[] { (Sintetico, g1.Id), (Real1, g1.Id), (Real2, g2.Id) })
                    {
                        db.Asistencias.Add(new Asistencia { SocioId = socio, GimnasioId = g, Instante = DateTime.UtcNow, Fecha = new DateOnly(2026, 3, 2), Metodo = MetodoAsistencia.Manual, RegistradoPorUsuarioId = dueno.Id });
                        db.CuotasSocio.Add(new CuotaSocio { SocioId = socio, GimnasioId = g, Anio = 2026, Mes = 3, Monto = 100, Vencimiento = new DateOnly(2026, 3, 10) });
                        db.RegistrosBienestarDiario.Add(new RegistroBienestarDiario { SocioId = socio, Fecha = new DateOnly(2026, 3, 2), Fatiga = 4, FrecuenciaCardiacaReposo = 60 });
                        var ejercicio = new Ejercicio { Id = Guid.NewGuid(), Nombre = $"Ej {socio}", ZonaObjetivo = "Legs", Origen = OrigenEjercicio.Proyecto };
                        db.Ejercicios.Add(ejercicio);
                        db.Sesiones.Add(new SesionEntrenamiento
                        {
                            Id = Guid.NewGuid(), SocioId = socio, Inicio = DateTime.UtcNow,
                            EjerciciosEjecutados = { new SesionEjercicio { Id = Guid.NewGuid(), EjercicioId = ejercicio.Id, Orden = 1,
                                SeriesRealizadas = { new SerieRealizada { Id = Guid.NewGuid(), OrdenSerie = 1, PesoLevantadoKg = 50, Repeticiones = 10, RIR = 2 } } } },
                        });
                    }
                    return Task.CompletedTask;
                });
                await EjecutarComoAdminAsync(Script(ClaveLectura, ClavePuntuacion));
            }

            public async Task DisposeAsync() => await Api.DisposeAsync();
        }
    }
}
