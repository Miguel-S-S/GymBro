using System.Net;
using System.Net.Http.Json;
using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GymBro.Tests.Integration
{
    /// <summary>
    /// POST /api/revisiones/{id}: la decisión numérica ("5" = Aprobada por Enum.TryParse) se rechaza con 400 y no cambia la rutina.
    /// Escenario SINTÉTICO de prueba: un gimnasio, su Dueño, un socio y una rutina pendiente de revisión por caso.
    /// </summary>
    public class RevisionDecisionTests : IClassFixture<RevisionDecisionTests.Escenario>
    {
        private readonly Escenario _e;
        public RevisionDecisionTests(Escenario e) { _e = e; }

        [Theory]
        [InlineData("5")]
        [InlineData("6")]
        [InlineData("7")]
        [InlineData("Aprobada,Editada")]
        [InlineData("PendienteRevision")]
        public async Task Una_decision_que_no_es_un_nombre_valido_responde_400_y_no_cambia_la_rutina(string decision)
        {
            var id = await _e.NuevaRutinaPendienteAsync();

            var r = await _e.Api.ClienteComo(_e.Dueno).PostAsJsonAsync($"/api/revisiones/{id}", new { decision, comentario = "Motivo sintético" });

            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
            Assert.Contains("Decisión inválida", await r.Content.ReadAsStringAsync());
            Assert.Equal(EstadoPrescripcion.PendienteRevision, await _e.EstadoAsync(id));
            Assert.Equal(0, await _e.RevisionesAsync(id));
        }

        [Fact]
        public async Task La_decision_por_nombre_se_sigue_aceptando()
        {
            var id = await _e.NuevaRutinaPendienteAsync();

            var r = await _e.Api.ClienteComo(_e.Dueno).PostAsJsonAsync($"/api/revisiones/{id}", new { decision = "aprobada" });

            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            Assert.Equal(EstadoPrescripcion.Aprobada, await _e.EstadoAsync(id));
            Assert.Equal(1, await _e.RevisionesAsync(id));
        }

        /// <summary>API + datos sintéticos compartidos.</summary>
        public class Escenario : IAsyncLifetime
        {
            public GymBroApiFactory Api { get; } = new();
            public Usuario Dueno { get; private set; } = null!;
            private Socio _socio = null!;

            public async Task InitializeAsync()
            {
                await Api.InitializeAsync();
                var g = new Gimnasio { Nombre = "Gimnasio (prueba revisión)", Codigo = "REV-1" };
                _socio = new Socio
                {
                    Id = Guid.NewGuid(), Nombre = "Socio", Apellido = "Test", Email = "socio@revision.test", Genero = "M",
                    FechaNacimiento = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc), GimnasioId = g.Id,
                    Objetivo = ObjetivoEntrenamiento.Mantenimiento, Nivel = NivelExperiencia.Principiante,
                };
                Dueno = new Usuario { Email = "dueno@revision.test", NombreCompleto = "Dueño", Rol = RolUsuario.AdministradorGimnasio, GimnasioId = g.Id, PasswordHash = "-" };
                await Api.ConDbAsync(db =>
                {
                    db.Gimnasios.Add(g);
                    db.Socios.Add(_socio);
                    db.Usuarios.Add(Dueno);
                    return Task.CompletedTask;
                });
            }

            public async Task<Guid> NuevaRutinaPendienteAsync()
            {
                var p = new PrescripcionEntrenamiento { SocioId = _socio.Id, Estado = EstadoPrescripcion.PendienteRevision, OrigenGeneracion = "Prueba sintética" };
                await Api.ConDbAsync(db => { db.PrescripcionesEntrenamiento.Add(p); return Task.CompletedTask; });
                return p.Id;
            }

            public async Task<EstadoPrescripcion> EstadoAsync(Guid id)
            {
                var estado = default(EstadoPrescripcion);
                await Api.ConDbAsync(async db => estado = (await db.PrescripcionesEntrenamiento.SingleAsync(p => p.Id == id)).Estado);
                return estado;
            }

            public async Task<int> RevisionesAsync(Guid id)
            {
                var n = 0;
                await Api.ConDbAsync(async db => n = await db.RevisionesPrescripcion.CountAsync(r => r.PrescripcionEntrenamientoId == id));
                return n;
            }

            public async Task DisposeAsync() => await Api.DisposeAsync();
        }
    }
}
