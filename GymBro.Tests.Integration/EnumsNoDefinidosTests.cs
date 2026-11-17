using System.Net;
using System.Net.Http.Json;
using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace GymBro.Tests.Integration
{
    /// <summary>
    /// fix-nivel-socio: ningún enum se persiste con un valor no definido (antes el 0 se guardaba como "0").
    /// Dos barreras: la guarda de SaveChanges (ReglaNegocioException, 400) y las restricciones CHECK de PostgreSQL.
    /// Escenario SINTÉTICO de prueba: un gimnasio, su Dueño y un socio con cuenta.
    /// </summary>
    public class EnumsNoDefinidosTests : IClassFixture<EnumsNoDefinidosTests.Escenario>
    {
        private readonly Escenario _e;
        public EnumsNoDefinidosTests(Escenario e) { _e = e; }

        // ---- Guarda de la aplicación (SaveChanges) ----

        [Theory]
        [InlineData(0, 3)]  // nivel sin asignar
        [InlineData(99, 3)] // nivel fuera del enum
        [InlineData(1, 0)]  // objetivo sin asignar
        [InlineData(1, 7)]  // objetivo fuera del enum
        public async Task Un_socio_con_nivel_u_objetivo_no_definido_no_se_guarda(int nivel, int objetivo)
        {
            var socio = Escenario.NuevoSocio($"guarda{nivel}-{objetivo}");
            socio.Nivel = (NivelExperiencia)nivel;
            socio.Objetivo = (ObjetivoEntrenamiento)objetivo;

            var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() =>
                _e.Api.ConDbAsync(db => { db.Socios.Add(socio); return Task.CompletedTask; }));
            Assert.StartsWith("Valor no válido para Socio.", ex.Message);

            var existe = true;
            await _e.Api.ConDbAsync(async db => existe = await db.Socios.IgnoreQueryFilters().AnyAsync(s => s.Id == socio.Id));
            Assert.False(existe);
        }

        [Fact]
        public async Task Modificar_un_socio_a_un_nivel_no_definido_se_rechaza()
        {
            var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => _e.Api.ConDbAsync(async db =>
            {
                var s = await db.Socios.SingleAsync(x => x.Id == _e.Socio.Id);
                s.Nivel = default;
            }));
            Assert.Equal("Valor no válido para Socio.Nivel.", ex.Message);
        }

        [Fact]
        public async Task Una_medicion_con_tipo_de_entrenamiento_no_definido_no_se_guarda_y_sin_dato_si()
        {
            var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => _e.Api.ConDbAsync(db =>
            {
                db.MedicionesFisicas.Add(new MedicionFisica { SocioId = _e.Socio.Id, PesoKg = 70, AlturaCm = 170, TipoEntrenamiento = default(TipoEntrenamiento) });
                return Task.CompletedTask;
            }));
            Assert.Equal("Valor no válido para MedicionFisica.TipoEntrenamiento.", ex.Message);

            // null = sin dato: permitido.
            await _e.Api.ConDbAsync(db =>
            {
                db.MedicionesFisicas.Add(new MedicionFisica { SocioId = _e.Socio.Id, PesoKg = 70, AlturaCm = 170, TipoEntrenamiento = null });
                return Task.CompletedTask;
            });
        }

        // ---- Restricciones CHECK de la base (aunque se saltee la aplicación) ----

        [Theory]
        [InlineData("UPDATE socios SET nivel = '0' WHERE id = {0}", "CK_Socio_Nivel")]
        [InlineData("UPDATE socios SET nivel = 'Experto' WHERE id = {0}", "CK_Socio_Nivel")]
        [InlineData("UPDATE socios SET objetivo = '0' WHERE id = {0}", "CK_Socio_Objetivo")]
        [InlineData("UPDATE mediciones_fisicas SET tipo_entrenamiento = '0' WHERE socio_id = {0}", "CK_MedicionFisica_TipoEntrenamiento")]
        public async Task La_base_rechaza_valores_de_enum_no_definidos(string sql, string restriccion)
        {
            await _e.AsegurarMedicionAsync();
            var ex = await Assert.ThrowsAsync<PostgresException>(() =>
                _e.Api.ConDbAsync(db => db.Database.ExecuteSqlRawAsync(sql, _e.Socio.Id)));
            Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
            Assert.Equal(restriccion, ex.ConstraintName);
        }

        // ---- Flujos reales por la API ----

        [Fact]
        public async Task El_alta_presencial_guarda_nivel_y_objetivo_definidos()
        {
            var r = await _e.Api.ClienteComo(_e.Dueno).PostAsJsonAsync("/api/Socio",
                new { nombre = "Alta", apellido = "Sintetica", email = "alta@enums.test", dni = "30111222" });
            Assert.Equal(HttpStatusCode.Created, r.StatusCode);

            Socio? guardado = null;
            await _e.Api.ConDbAsync(async db => guardado = await db.Socios.SingleAsync(s => s.Email == "alta@enums.test"));
            Assert.Equal(NivelExperiencia.Principiante, guardado!.Nivel);
            Assert.Equal(ObjetivoEntrenamiento.Mantenimiento, guardado.Objetivo);

            var texto = "";
            await _e.Api.ConDbAsync(async db => texto = await db.Database
                .SqlQueryRaw<string>("SELECT nivel || '/' || objetivo AS \"Value\" FROM socios WHERE id = {0}", guardado.Id).SingleAsync());
            Assert.Equal("Principiante/Mantenimiento", texto);
        }

        [Fact]
        public async Task La_medicion_desde_la_app_queda_sin_tipo_de_entrenamiento_y_no_con_cero()
        {
            var cliente = _e.Api.ClienteComo(_e.UsuarioSocio);
            var r = await cliente.PostAsJsonAsync("/api/Medicion", new { socioId = _e.Socio.Id, pesoKg = 81.5m, alturaCm = 178m });
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);

            var tipos = new List<string?>();
            await _e.Api.ConDbAsync(async db => tipos = await db.Database
                .SqlQueryRaw<string?>("SELECT tipo_entrenamiento AS \"Value\" FROM mediciones_fisicas WHERE socio_id = {0} AND peso_kg = 81.5", _e.Socio.Id)
                .ToListAsync());
            Assert.Equal(new string?[] { null }, tipos);

            var lista = await cliente.GetFromJsonAsync<List<Medicion>>($"/api/Medicion?socioId={_e.Socio.Id}");
            Assert.All(lista!, m => Assert.NotEqual("0", m.TipoEntrenamiento));
            Assert.Contains(lista!, m => m.PesoKg == 81.5m && m.TipoEntrenamiento is null);
        }

        private sealed record Medicion(decimal PesoKg, string? TipoEntrenamiento);

        /// <summary>API + datos sintéticos compartidos.</summary>
        public class Escenario : IAsyncLifetime
        {
            public GymBroApiFactory Api { get; } = new();
            public Socio Socio { get; private set; } = null!;
            public Usuario UsuarioSocio { get; private set; } = null!;
            public Usuario Dueno { get; private set; } = null!;

            public static Socio NuevoSocio(string clave) => new()
            {
                Id = Guid.NewGuid(), Nombre = clave, Apellido = "Test", Email = $"{clave}@enums.test", Genero = "F",
                FechaNacimiento = new DateTime(1995, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                Objetivo = ObjetivoEntrenamiento.Mantenimiento, Nivel = NivelExperiencia.Principiante,
            };

            public async Task InitializeAsync()
            {
                await Api.InitializeAsync();
                var g = new Gimnasio { Nombre = "Gimnasio (prueba enums)", Codigo = "ENUM-1" };
                Socio = NuevoSocio("socio");
                Socio.GimnasioId = g.Id;
                UsuarioSocio = new Usuario { Email = Socio.Email, NombreCompleto = "Socio Test", Rol = RolUsuario.Socio, SocioId = Socio.Id, PasswordHash = "-" };
                Dueno = new Usuario { Email = "dueno@enums.test", NombreCompleto = "Dueño", Rol = RolUsuario.AdministradorGimnasio, GimnasioId = g.Id, PasswordHash = "-" };
                await Api.ConDbAsync(db =>
                {
                    db.Gimnasios.Add(g);
                    db.Socios.Add(Socio);
                    db.Usuarios.AddRange(UsuarioSocio, Dueno);
                    return Task.CompletedTask;
                });
            }

            /// <summary>Garantiza al menos una medición del socio para la prueba de la restricción.</summary>
            public Task AsegurarMedicionAsync() => Api.ConDbAsync(async db =>
            {
                if (!await db.MedicionesFisicas.AnyAsync(m => m.SocioId == Socio.Id))
                    db.MedicionesFisicas.Add(new MedicionFisica { SocioId = Socio.Id, PesoKg = 70, AlturaCm = 170 });
            });

            public async Task DisposeAsync() => await Api.DisposeAsync();
        }
    }
}
