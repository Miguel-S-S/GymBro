using System.Net;
using System.Net.Http.Json;
using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using Xunit;

namespace GymBro.Tests.Integration
{
    /// <summary>
    /// Criterio 4.6 de la Etapa 8: un Socio no lee ni escribe sesiones de otro socio.
    /// Los dos socios son del MISMO gimnasio (el caso más exigente: el filtro de tenant por gimnasio no alcanza).
    /// Datos sintéticos de prueba, creados en un contenedor descartable.
    /// </summary>
    public class SesionesAislamientoTests : IClassFixture<GymBroApiFactory>, IAsyncLifetime
    {
        private readonly GymBroApiFactory _api;

        private readonly Gimnasio _gimnasio = new() { Nombre = "Gimnasio de prueba", Codigo = "TEST-INT" };
        private Socio _socioA = null!, _socioB = null!;
        private Usuario _usuarioA = null!;
        private Ejercicio _ejercicio = null!;
        private PrescripcionEntrenamiento _prescripcionA = null!, _prescripcionB = null!;
        private SesionEntrenamiento _sesionB = null!;

        public SesionesAislamientoTests(GymBroApiFactory api) { _api = api; }

        public async Task InitializeAsync()
        {
            var sufijo = Guid.NewGuid().ToString("N")[..8];
            _gimnasio.Codigo = $"T-{sufijo}";
            _socioA = NuevoSocio("Ana", $"ana-{sufijo}@test.local");
            _socioB = NuevoSocio("Bruno", $"bruno-{sufijo}@test.local");
            // Igual que InvitacionService: la cuenta de Socio lleva SocioId y no GimnasioId.
            _usuarioA = new Usuario { Email = _socioA.Email, NombreCompleto = "Ana Test", Rol = RolUsuario.Socio, SocioId = _socioA.Id, PasswordHash = "-" };
            _ejercicio = new Ejercicio { Nombre = $"Press de prueba {sufijo}", ZonaObjetivo = "Chest", MusculoPrincipal = "Chest", Equipamiento = "Barbell", Dificultad = 1 };
            _prescripcionA = new PrescripcionEntrenamiento { SocioId = _socioA.Id, Estado = EstadoPrescripcion.Aprobada, OrigenGeneracion = "TEST" };
            _prescripcionB = new PrescripcionEntrenamiento { SocioId = _socioB.Id, Estado = EstadoPrescripcion.Aprobada, OrigenGeneracion = "TEST" };
            _sesionB = new SesionEntrenamiento { Id = Guid.NewGuid(), SocioId = _socioB.Id, Inicio = DateTime.UtcNow.AddDays(-1), Fin = DateTime.UtcNow.AddDays(-1).AddHours(1) };

            await _api.ConDbAsync(db =>
            {
                db.Gimnasios.Add(_gimnasio);
                db.Socios.AddRange(_socioA, _socioB);
                db.Usuarios.Add(_usuarioA);
                db.Ejercicios.Add(_ejercicio);
                db.PrescripcionesEntrenamiento.AddRange(_prescripcionA, _prescripcionB);
                db.Sesiones.Add(_sesionB);
                return Task.CompletedTask;
            });
        }

        public Task DisposeAsync() => Task.CompletedTask;

        private Socio NuevoSocio(string nombre, string email) => new()
        {
            Id = Guid.NewGuid(), Nombre = nombre, Apellido = "Test", Email = email, Genero = "M",
            FechaNacimiento = new DateTime(1995, 1, 1, 0, 0, 0, DateTimeKind.Utc), GimnasioId = _gimnasio.Id,
            Objetivo = ObjetivoEntrenamiento.Mantenimiento, Nivel = NivelExperiencia.Principiante,
            ConsentimientoDatosSaludFecha = DateTime.UtcNow,
        };

        private object Sesion(Guid socioId, Guid? prescripcionId) => new
        {
            socioId,
            prescripcionEntrenamientoId = prescripcionId,
            inicio = DateTime.UtcNow.AddHours(-1),
            fin = DateTime.UtcNow,
            ejercicios = new[]
            {
                new { ejercicioId = _ejercicio.Id, orden = 1, seriesRealizadas = new[]
                {
                    new { ordenSerie = 1, pesoLevantadoKg = 40m, repeticiones = 10, rir = 2, esCalentamiento = false, tiempoDescansoSegundos = 90 }
                } }
            }
        };

        // ---- Lectura ----
        [Fact]
        public async Task SocioA_NoListaLasSesionesDeB()
        {
            var r = await _api.ClienteComo(_usuarioA).GetAsync($"/api/Sesiones?socioId={_socioB.Id}");
            Assert.Equal(HttpStatusCode.NotFound, r.StatusCode); // T7: el socio B no es visible para A
            await AssertSinDatosDeB(r);
        }

        [Fact]
        public async Task SocioA_NoLeeUnaSesionDeB()
        {
            var r = await _api.ClienteComo(_usuarioA).GetAsync($"/api/Sesiones/{_sesionB.Id}");
            Assert.Equal(HttpStatusCode.NotFound, r.StatusCode); // el filtro de tenant la oculta
        }

        [Fact]
        public async Task SocioA_NoLeeLaComparacionDeUnaSesionDeB()
        {
            var r = await _api.ClienteComo(_usuarioA).GetAsync($"/api/Sesiones/{_sesionB.Id}/comparacion");
            Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        }

        // ---- Escritura ----
        [Fact]
        public async Task SocioA_NoRegistraUnaSesionAnombreDeB()
        {
            var r = await _api.ClienteComo(_usuarioA).PostAsJsonAsync("/api/Sesiones", Sesion(_socioB.Id, null));
            Assert.Equal(HttpStatusCode.NotFound, r.StatusCode); // T7: el socio B no es visible para A
        }

        private async Task AssertSinDatosDeB(HttpResponseMessage r)
        {
            var cuerpo = await r.Content.ReadAsStringAsync();
            Assert.DoesNotContain(_socioB.Nombre, cuerpo);
            Assert.DoesNotContain(_sesionB.Id.ToString(), cuerpo);
        }

        [Fact]
        public async Task SocioA_NoVinculaSuSesionALaPrescripcionDeB()
        {
            var r = await _api.ClienteComo(_usuarioA).PostAsJsonAsync("/api/Sesiones", Sesion(_socioA.Id, _prescripcionB.Id));
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        }

        // ---- Control positivo: el token y el flujo funcionan para lo propio ----
        [Fact]
        public async Task SocioA_RegistraYLeeSuPropiaSesionVinculada()
        {
            var cliente = _api.ClienteComo(_usuarioA);

            var alta = await cliente.PostAsJsonAsync("/api/Sesiones", Sesion(_socioA.Id, _prescripcionA.Id));
            Assert.Equal(HttpStatusCode.Created, alta.StatusCode);

            var lista = await cliente.GetAsync($"/api/Sesiones?socioId={_socioA.Id}");
            Assert.Equal(HttpStatusCode.OK, lista.StatusCode);
        }

        [Fact]
        public async Task SinToken_Responde401()
        {
            var r = await _api.CreateClient().GetAsync($"/api/Sesiones?socioId={_socioA.Id}");
            Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
        }
    }
}
