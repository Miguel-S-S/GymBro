using System.Text.RegularExpressions;
using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Domain.Rules;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Xunit;

namespace GymBro.Tests.Integration
{
    /// <summary>
    /// Etapa 9a-2b, sección 6: los scripts de docs/pfc/scripts se ejecutan tal cual contra PostgreSQL descartable, solo
    /// reemplazando los valores de los parámetros del bloque DECLARE (y el ROLLBACK final por COMMIT cuando se verifica
    /// el efecto). Escenario SINTÉTICO de prueba, sin datos reales.
    /// </summary>
    public class ScriptsAdministracionTests : IClassFixture<ScriptsAdministracionTests.Escenario>
    {
        private readonly Escenario _e;
        public ScriptsAdministracionTests(Escenario e) { _e = e; }

        private static string Carpeta()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "GymBro.slnx"))) dir = dir.Parent;
            return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("No se encontró la raíz del repositorio."), "docs", "pfc", "scripts");
        }

        /// <summary>Lee el script, fija los parámetros indicados (literales SQL) y, si se pide, cambia el ROLLBACK por COMMIT.</summary>
        private static string Preparar(string archivo, Dictionary<string, string> parametros, bool commit)
        {
            var sql = File.ReadAllText(Path.Combine(Carpeta(), archivo));
            foreach (var (nombre, valor) in parametros)
            {
                var patron = new Regex($@"(\b{nombre}\s+\w+\s*:=\s*)[^;]+;");
                Assert.True(patron.IsMatch(sql), $"{archivo}: no tiene el parámetro {nombre}");
                sql = patron.Replace(sql, m => m.Groups[1].Value + valor + ";", 1);
            }
            if (commit)
            {
                Assert.Contains("ROLLBACK;   -- por defecto no se guarda nada", sql);
                sql = sql.Replace("ROLLBACK;   -- por defecto no se guarda nada", "COMMIT;");
            }
            return sql;
        }

        /// <summary>Ejecuta el texto completo con Npgsql (sin el formateo de EF, que interpretaría las llaves).</summary>
        private async Task EjecutarAsync(string sql)
        {
            await _e.Api.ConDbAsync(async db =>
            {
                var conexion = db.Database.GetDbConnection();
                await conexion.OpenAsync();
                try
                {
                    await using var cmd = conexion.CreateCommand();
                    cmd.CommandText = sql;
                    await cmd.ExecuteNonQueryAsync();
                }
                finally { await conexion.CloseAsync(); }
            });
        }

        private Task CorrerAsync(string archivo, Dictionary<string, string> p, bool commit = true) =>
            EjecutarAsync(Preparar(archivo, new Dictionary<string, string>(p) { ["p_admin_usuario_id"] = U(_e.Admin.Id) }, commit));

        private static string U(Guid id) => $"'{id}'";
        private static string T(string s) => $"'{s}'";

        private async Task<T?> LeerAsync<T>(Func<GymBro.Infrastructure.Persistence.GymBroDbContext, Task<T>> f)
        {
            T? r = default;
            await _e.Api.ConDbAsync(async db => r = await f(db));
            return r;
        }

        private Task<int> AuditoriaAsync(string accion, string entidadId) =>
            LeerAsync(db => db.AuditLogs.CountAsync(a => a.Accion == accion && a.EntidadId == entidadId && a.Rol == "SuperAdministrador"
                                                         && a.UsuarioId == _e.Admin.Id))!;

        // ---- Comportamiento por defecto ----

        [Theory]
        [InlineData("admin-gimnasio.sql")]
        [InlineData("admin-usuario.sql")]
        [InlineData("admin-socio.sql")]
        [InlineData("admin-invitacion.sql")]
        [InlineData("admin-vinculacion.sql")]
        [InlineData("admin-asignacion.sql")]
        [InlineData("admin-dosificacion-matriz.sql")]
        [InlineData("admin-ejercicio-global.sql")]
        public async Task Sin_completar_los_parametros_el_script_se_rechaza_y_termina_en_ROLLBACK(string archivo)
        {
            var sql = File.ReadAllText(Path.Combine(Carpeta(), archivo));
            Assert.Contains("BEGIN;", sql);
            Assert.Contains("ROLLBACK;   -- por defecto no se guarda nada", sql);
            Assert.Contains("-- COMMIT;", sql);
            Assert.Contains("INSERT INTO audit_logs", sql);
            var ex = await Record.ExceptionAsync(() => EjecutarAsync(sql));
            Assert.NotNull(ex);
            Assert.Contains("no es un Administrador de la Plataforma vigente", ex!.Message);
        }

        [Fact]
        public async Task Con_parametros_validos_y_el_ROLLBACK_por_defecto_no_queda_ningun_cambio()
        {
            await CorrerAsync("admin-gimnasio.sql", new() { ["p_gimnasio_id"] = U(_e.G1.Id), ["p_nuevo_nombre"] = T("No debe quedar") }, commit: false);
            Assert.Equal("Scripts 1", await LeerAsync(db => db.Gimnasios.Where(g => g.Id == _e.G1.Id).Select(g => g.Nombre).SingleAsync()));
            Assert.Equal(0, await AuditoriaAsync("SCRIPT_MODIFICACION_GIMNASIO", _e.G1.Id.ToString()));
        }

        // ---- Un caso por script, con COMMIT ----

        [Fact]
        public async Task Gimnasio_renombra_y_cambia_la_politica_con_auditoria()
        {
            await CorrerAsync("admin-gimnasio.sql", new() { ["p_gimnasio_id"] = U(_e.G2.Id), ["p_nuevo_nombre"] = T("Scripts 2 renombrado"), ["p_requiere_revision"] = "true" });
            var g = await LeerAsync(db => db.Gimnasios.SingleAsync(x => x.Id == _e.G2.Id));
            Assert.Equal(("Scripts 2 renombrado", true), (g!.Nombre, g.RequiereAprobacionRutinas));
            Assert.Equal(1, await AuditoriaAsync("SCRIPT_MODIFICACION_GIMNASIO", _e.G2.Id.ToString()));
        }

        [Fact]
        public async Task Usuario_desactiva_da_de_baja_un_trainer_con_sus_asignaciones_y_no_toca_socios()
        {
            await CorrerAsync("admin-usuario.sql", new() { ["p_usuario_id"] = U(_e.Dueno.Id), ["p_operacion"] = T("DESACTIVAR") });
            Assert.False((await LeerAsync(db => db.Usuarios.IgnoreQueryFilters().SingleAsync(u => u.Id == _e.Dueno.Id)))!.Activo);

            await CorrerAsync("admin-usuario.sql", new() { ["p_usuario_id"] = U(_e.TrainerBaja.Id), ["p_operacion"] = T("BAJA") });
            var t = await LeerAsync(db => db.Usuarios.IgnoreQueryFilters().SingleAsync(u => u.Id == _e.TrainerBaja.Id));
            Assert.NotNull(t!.FechaBaja);
            Assert.Equal(0, await LeerAsync(db => db.Asignaciones.IgnoreQueryFilters().CountAsync(a => a.EntrenadorUsuarioId == _e.TrainerBaja.Id)));
            Assert.Equal(1, await AuditoriaAsync("SCRIPT_BAJA_USUARIO", _e.TrainerBaja.Id.ToString()));

            var socio = await Record.ExceptionAsync(() => CorrerAsync("admin-usuario.sql", new() { ["p_usuario_id"] = U(_e.CuentaSocio.Id), ["p_operacion"] = T("DESACTIVAR") }));
            Assert.Contains("solo administra Duenos y Personal Trainers", socio!.Message);
            var repetido = await Record.ExceptionAsync(() => CorrerAsync("admin-usuario.sql", new()
                { ["p_usuario_id"] = U(_e.Trainer.Id), ["p_operacion"] = T("CORREGIR"), ["p_nuevo_email"] = T(_e.Dueno.Email.ToUpperInvariant()) }));
            Assert.Contains("Ya existe una cuenta con ese email", repetido!.Message);
        }

        [Fact]
        public async Task Socio_estado_correccion_de_DNI_y_baja_sin_cuenta()
        {
            await CorrerAsync("admin-socio.sql", new() { ["p_socio_id"] = U(_e.SocioConCuenta.Id), ["p_operacion"] = T("ESTADO"), ["p_nuevo_estado"] = T("Inactivo") });
            Assert.Equal(EstadoSocio.Inactivo, (await LeerAsync(db => db.Socios.SingleAsync(s => s.Id == _e.SocioConCuenta.Id)))!.Estado);

            await CorrerAsync("admin-socio.sql", new() { ["p_socio_id"] = U(_e.SocioSinCuenta.Id), ["p_operacion"] = T("CORREGIR"), ["p_nuevo_dni"] = T("30.111.222") });
            Assert.Equal("30111222", (await LeerAsync(db => db.Socios.SingleAsync(s => s.Id == _e.SocioSinCuenta.Id)))!.Dni);

            await CorrerAsync("admin-socio.sql", new() { ["p_socio_id"] = U(_e.SocioSinCuenta.Id), ["p_operacion"] = T("BAJA") });
            var baja = await LeerAsync(db => db.Socios.IgnoreQueryFilters().SingleAsync(s => s.Id == _e.SocioSinCuenta.Id));
            Assert.Equal((true, (string?)null), (baja!.FechaBaja != null, baja.Dni));
            Assert.NotNull((await LeerAsync(db => db.Invitaciones.IgnoreQueryFilters().SingleAsync(i => i.Id == _e.InvitacionSinCuenta.Id)))!.RevocadaUtc);
            Assert.Equal(1, await AuditoriaAsync("SCRIPT_BAJA_SOCIO", _e.SocioSinCuenta.Id.ToString()));

            // Con cuenta, el email no se corrige por script (regla de la aplicación).
            var ex = await Record.ExceptionAsync(() => CorrerAsync("admin-socio.sql", new()
                { ["p_socio_id"] = U(_e.SocioConCuenta.Id), ["p_operacion"] = T("CORREGIR"), ["p_nuevo_email"] = T("otro@scripts.test") }));
            Assert.Contains("ya activo su cuenta", ex!.Message);
        }

        [Fact]
        public async Task Invitacion_revoca_las_vigentes()
        {
            await CorrerAsync("admin-invitacion.sql", new() { ["p_socio_id"] = U(_e.SocioInvitado.Id) });
            Assert.NotNull((await LeerAsync(db => db.Invitaciones.IgnoreQueryFilters().SingleAsync(i => i.Id == _e.InvitacionVigente.Id)))!.RevocadaUtc);
            Assert.Equal(1, await AuditoriaAsync("SCRIPT_REVOCAR_INVITACION", _e.SocioInvitado.Id.ToString()));
        }

        [Fact]
        public async Task Vinculacion_aprobada_vincula_al_socio_si_el_DNI_coincide()
        {
            await CorrerAsync("admin-vinculacion.sql", new() { ["p_solicitud_id"] = U(_e.Solicitud.Id), ["p_operacion"] = T("APROBAR") });
            Assert.Equal(_e.G1.Id, (await LeerAsync(db => db.Socios.IgnoreQueryFilters().SingleAsync(s => s.Id == _e.SocioIndependiente.Id)))!.GimnasioId);
            var s = await LeerAsync(db => db.SolicitudesVinculacion.IgnoreQueryFilters().SingleAsync(x => x.Id == _e.Solicitud.Id));
            Assert.Equal((EstadoSolicitudVinculacion.Aprobada, _e.Admin.Id), (s!.Estado, s.ResueltaPorUsuarioId));
            Assert.Equal(1, await AuditoriaAsync("SCRIPT_VINCULACION_APROBADA", _e.Solicitud.Id.ToString()));
        }

        [Fact]
        public async Task Asignacion_se_crea_y_se_quita()
        {
            var p = new Dictionary<string, string> { ["p_entrenador_usuario_id"] = U(_e.Trainer.Id), ["p_socio_id"] = U(_e.SocioConCuenta.Id) };
            await CorrerAsync("admin-asignacion.sql", new(p) { ["p_operacion"] = T("ASIGNAR") });
            Assert.Equal(1, await LeerAsync(db => db.Asignaciones.IgnoreQueryFilters().CountAsync(a => a.EntrenadorUsuarioId == _e.Trainer.Id && a.SocioId == _e.SocioConCuenta.Id)));
            await CorrerAsync("admin-asignacion.sql", new(p) { ["p_operacion"] = T("DESASIGNAR") });
            Assert.Equal(0, await LeerAsync(db => db.Asignaciones.IgnoreQueryFilters().CountAsync(a => a.EntrenadorUsuarioId == _e.Trainer.Id && a.SocioId == _e.SocioConCuenta.Id)));
            // Se puede volver a asignar (por eso se borra en lugar de una baja lógica).
            await CorrerAsync("admin-asignacion.sql", new(p) { ["p_operacion"] = T("ASIGNAR") });
        }

        [Fact]
        public async Task Dosificacion_y_matriz_se_modifican_y_dan_de_baja()
        {
            await CorrerAsync("admin-dosificacion-matriz.sql", new()
                { ["p_operacion"] = T("DOSIS_MODIFICAR"), ["p_dosis_id"] = U(_e.Dosis.Id), ["p_series_min"] = "2", ["p_reps_max"] = "15" });
            var d = await LeerAsync(db => db.Dosificaciones.SingleAsync(x => x.Id == _e.Dosis.Id));
            Assert.Equal((2, 4, 8, 15), (d!.SeriesMinimas, d.SeriesMaximas, d.RepeticionesMinimas, d.RepeticionesMaximas));

            var invalida = await Record.ExceptionAsync(() => CorrerAsync("admin-dosificacion-matriz.sql", new()
                { ["p_operacion"] = T("DOSIS_MODIFICAR"), ["p_dosis_id"] = U(_e.Dosis.Id), ["p_series_min"] = "9" }));
            Assert.Contains("Valores invalidos", invalida!.Message);

            await CorrerAsync("admin-dosificacion-matriz.sql", new() { ["p_operacion"] = T("MATRIZ_BAJA"), ["p_matriz_id"] = _e.MatrizId.ToString() });
            Assert.NotNull((await LeerAsync(db => db.MatricesRecomendacion.IgnoreQueryFilters().SingleAsync(m => m.Id == _e.MatrizId)))!.FechaBaja);
            Assert.Equal(1, await AuditoriaAsync("SCRIPT_MATRIZ_BAJA", _e.MatrizId.ToString()));
        }

        [Fact]
        public async Task Ejercicio_global_se_modifica_conserva_origen_y_deja_el_embedding_pendiente()
        {
            await CorrerAsync("admin-ejercicio-global.sql", new()
                { ["p_ejercicio_id"] = U(_e.Global.Id), ["p_nombre"] = T("Global por script (v2)"), ["p_dificultad"] = "3" });
            var e = await LeerAsync(db => db.Ejercicios.IgnoreQueryFilters().SingleAsync(x => x.Id == _e.Global.Id));
            Assert.Equal(("Global por script (v2)", 3, OrigenEjercicio.Dataset, (Guid?)null), (e!.Nombre, e.Dificultad, e.Origen, e.GimnasioId));
            Assert.Null(e.Embedding);
            Assert.Equal(1, await AuditoriaAsync("SCRIPT_EDICION_EJERCICIO_GLOBAL", _e.Global.Id.ToString()));

            var propio = await Record.ExceptionAsync(() => CorrerAsync("admin-ejercicio-global.sql", new()
                { ["p_ejercicio_id"] = U(_e.Propio.Id), ["p_dificultad"] = "2" }));
            Assert.Contains("solo para globales", propio!.Message);
        }

        // ---- Escenario ----

        public class Escenario : IAsyncLifetime
        {
            public GymBroApiFactory Api { get; } = new();
            public Gimnasio G1 { get; } = new() { Nombre = "Scripts 1", Codigo = "SC-1" };
            public Gimnasio G2 { get; } = new() { Nombre = "Scripts 2", Codigo = "SC-2" };
            public Usuario Admin { get; } = new() { Email = "plataforma@scripts.test", NombreCompleto = "Plataforma", Rol = RolUsuario.SuperAdministrador, PasswordHash = "-" };
            public Usuario Dueno { get; private set; } = null!;
            public Usuario Trainer { get; private set; } = null!;
            public Usuario TrainerBaja { get; private set; } = null!;
            public Usuario CuentaSocio { get; private set; } = null!;
            public Socio SocioConCuenta { get; private set; } = null!;
            public Socio SocioSinCuenta { get; private set; } = null!;
            public Socio SocioInvitado { get; private set; } = null!;
            public Socio SocioIndependiente { get; private set; } = null!;
            public InvitacionSocio InvitacionSinCuenta { get; private set; } = null!;
            public InvitacionSocio InvitacionVigente { get; private set; } = null!;
            public SolicitudVinculacion Solicitud { get; private set; } = null!;
            public DosificacionVolumetrica Dosis { get; private set; } = null!;
            public int MatrizId { get; private set; }
            public Ejercicio Global { get; private set; } = null!;
            public Ejercicio Propio { get; private set; } = null!;

            public async Task InitializeAsync()
            {
                await Api.InitializeAsync();
                Dueno = new Usuario { Email = "dueno@scripts.test", NombreCompleto = "Dueno", Rol = RolUsuario.AdministradorGimnasio, GimnasioId = G1.Id, PasswordHash = "-" };
                Trainer = new Usuario { Email = "trainer@scripts.test", NombreCompleto = "Trainer", Rol = RolUsuario.Entrenador, GimnasioId = G1.Id, PasswordHash = "-" };
                TrainerBaja = new Usuario { Email = "trainer.baja@scripts.test", NombreCompleto = "Trainer baja", Rol = RolUsuario.Entrenador, GimnasioId = G1.Id, PasswordHash = "-" };
                SocioConCuenta = NuevoSocio("con.cuenta@scripts.test", G1.Id, "20111222");
                SocioSinCuenta = NuevoSocio("sin.cuenta@scripts.test", G1.Id, "20333444");
                SocioInvitado = NuevoSocio("invitado@scripts.test", G1.Id, "20555666");
                SocioIndependiente = NuevoSocio("independiente@scripts.test", null, "20777888");
                CuentaSocio = new Usuario { Email = SocioConCuenta.Email, NombreCompleto = "Socio", Rol = RolUsuario.Socio, SocioId = SocioConCuenta.Id, PasswordHash = "-" };
                InvitacionSinCuenta = new InvitacionSocio { GimnasioId = G1.Id, SocioId = SocioSinCuenta.Id, TokenHash = new string('a', 64), ExpiraUtc = DateTime.UtcNow.AddDays(3) };
                InvitacionVigente = new InvitacionSocio { GimnasioId = G1.Id, SocioId = SocioInvitado.Id, TokenHash = new string('b', 64), ExpiraUtc = DateTime.UtcNow.AddDays(3) };
                Solicitud = new SolicitudVinculacion { SocioId = SocioIndependiente.Id, GimnasioId = G1.Id, NombreSocio = "Independiente", DniDeclarado = "20777888" };
                Dosis = new DosificacionVolumetrica
                {
                    Id = Guid.NewGuid(), ParteCuerpo = "Chest", SubzonaObjetivo = "Scripts", ZonaObjetivo = "Chest",
                    SeriesMinimas = 3, SeriesMaximas = 4, RepeticionesMinimas = 8, RepeticionesMaximas = 12,
                };
                var matriz = new MatrizRecomendacion
                {
                    Genero = "F", Edad = 30, CategoriaIMC = CategoriaIMC.Normal, Objetivo = ObjetivoEntrenamiento.Mantenimiento,
                    CategoriaRutina = "Prueba", SugerenciaDieta = "Prueba", RecomendacionGeneral = "Prueba",
                };
                Global = new Ejercicio
                {
                    Id = Guid.NewGuid(), Nombre = "Global por script", Equipamiento = "Barbell", ZonaObjetivo = "Chest", MusculoPrincipal = "Pectoral",
                    Ejecucion = "-", Dificultad = 1, TipoEquipamientoId = CatalogoEquipamiento.IdDe(CatalogoEquipamiento.Barra),
                    Embedding = new Vector(Enumerable.Repeat(0.01f, 768).ToArray()),
                };
                Propio = new Ejercicio
                {
                    Id = Guid.NewGuid(), Nombre = "Propio no global", Equipamiento = "Peso corporal", ZonaObjetivo = "Chest", MusculoPrincipal = "Pectoral",
                    Ejecucion = "-", Dificultad = 1, Origen = OrigenEjercicio.Gimnasio, GimnasioId = G1.Id,
                    TipoEquipamientoId = CatalogoEquipamiento.IdDe(CatalogoEquipamiento.PesoCorporal),
                };

                await Api.ConDbAsync(db =>
                {
                    db.Gimnasios.AddRange(G1, G2);
                    db.Socios.AddRange(SocioConCuenta, SocioSinCuenta, SocioInvitado, SocioIndependiente);
                    db.Usuarios.AddRange(Admin, Dueno, Trainer, TrainerBaja, CuentaSocio);
                    db.Asignaciones.Add(new AsignacionEntrenadorSocio { GimnasioId = G1.Id, EntrenadorUsuarioId = TrainerBaja.Id, SocioId = SocioConCuenta.Id });
                    db.Invitaciones.AddRange(InvitacionSinCuenta, InvitacionVigente);
                    db.SolicitudesVinculacion.Add(Solicitud);
                    db.Dosificaciones.Add(Dosis);
                    db.MatricesRecomendacion.Add(matriz);
                    db.Ejercicios.AddRange(Global, Propio);
                    return Task.CompletedTask;
                });
                MatrizId = matriz.Id;
            }

            public async Task DisposeAsync() => await Api.DisposeAsync();

            private static Socio NuevoSocio(string email, Guid? gimnasioId, string dni) => new()
            {
                Id = Guid.NewGuid(), Nombre = "Socio", Apellido = "Scripts", Email = email, Dni = dni, Genero = "F",
                FechaNacimiento = new DateTime(1995, 1, 1, 0, 0, 0, DateTimeKind.Utc), GimnasioId = gimnasioId,
                Objetivo = ObjetivoEntrenamiento.Mantenimiento, Nivel = NivelExperiencia.Principiante,
            };
        }
    }
}
