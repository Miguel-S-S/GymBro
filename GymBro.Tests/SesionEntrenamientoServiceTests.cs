using GymBro.Application.DTOs;
using GymBro.Application.Services;
using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Domain.Exceptions;
using GymBro.Domain.Repositories;
using Xunit;

namespace GymBro.Tests
{
    /// <summary>
    /// B1 (Etapa 8): al registrar una sesión vinculada a una prescripción, la prescripción debe ser del mismo socio
    /// y estar vigente (Aprobada o Editada). Repositorios en memoria.
    /// </summary>
    public class SesionEntrenamientoServiceTests
    {
        private static readonly Guid SocioA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
        private static readonly Guid SocioB = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
        private static readonly Guid EjercicioId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");

        private readonly FakeSesionRepository _sesiones = new();
        private readonly FakePrescripcionRepository _prescripciones = new();
        private readonly FakeAcceso _acceso = new();

        private SesionEntrenamientoService Servicio() => new(
            _sesiones, new FakeSocioRepository(SocioA, SocioB), new FakeEjercicioRepository(EjercicioId), _prescripciones, _acceso);

        private PrescripcionEntrenamiento Prescripcion(Guid socioId, EstadoPrescripcion estado)
        {
            var p = new PrescripcionEntrenamiento { SocioId = socioId, Estado = estado };
            _prescripciones.Items.Add(p);
            return p;
        }

        private static RegistrarSesionEntrenamientoRequest Request(Guid socioId, Guid? prescripcionId, int? rir = 2) => new()
        {
            SocioId = socioId,
            PrescripcionEntrenamientoId = prescripcionId,
            Inicio = new DateTime(2026, 11, 16, 18, 0, 0, DateTimeKind.Utc),
            Fin = new DateTime(2026, 11, 16, 19, 0, 0, DateTimeKind.Utc),
            Ejercicios =
            {
                new SesionEjercicioRequestDto
                {
                    EjercicioId = EjercicioId, Orden = 1,
                    SeriesRealizadas = { new SerieRealizadaRequestDto { OrdenSerie = 1, PesoLevantadoKg = 40, Repeticiones = 10, RIR = rir, TiempoDescansoSegundos = 90 } }
                }
            }
        };

        [Theory]
        [InlineData(EstadoPrescripcion.Aprobada)]
        [InlineData(EstadoPrescripcion.Editada)]
        public async Task PrescripcionVigenteDelMismoSocio_RegistraLaSesionVinculada(EstadoPrescripcion estado)
        {
            var p = Prescripcion(SocioA, estado);

            var sesion = await Servicio().RegistrarSesionAsync(Request(SocioA, p.Id));

            Assert.Equal(p.Id, sesion.PrescripcionEntrenamientoId);
            Assert.Single(_sesiones.Items);
        }

        [Fact]
        public async Task PrescripcionDeOtroSocio_SeRechazaYNoSeGuarda()
        {
            var deB = Prescripcion(SocioB, EstadoPrescripcion.Aprobada);

            var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => Servicio().RegistrarSesionAsync(Request(SocioA, deB.Id)));

            Assert.Contains("no pertenece", ex.Message);
            Assert.Empty(_sesiones.Items);
        }

        [Theory]
        [InlineData(EstadoPrescripcion.Creada)]
        [InlineData(EstadoPrescripcion.Aceptada)]
        [InlineData(EstadoPrescripcion.Cancelada)]
        [InlineData(EstadoPrescripcion.PendienteRevision)]
        [InlineData(EstadoPrescripcion.Rechazada)]
        public async Task PrescripcionNoVigente_SeRechazaYNoSeGuarda(EstadoPrescripcion estado)
        {
            var p = Prescripcion(SocioA, estado);

            var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => Servicio().RegistrarSesionAsync(Request(SocioA, p.Id)));

            Assert.Contains("no está vigente", ex.Message);
            Assert.Empty(_sesiones.Items);
        }

        [Fact]
        public async Task PrescripcionInexistente_LanzaArgumentException()
        {
            await Assert.ThrowsAsync<ArgumentException>(() => Servicio().RegistrarSesionAsync(Request(SocioA, Guid.NewGuid())));
            Assert.Empty(_sesiones.Items);
        }

        [Fact]
        public async Task SesionLibre_SinPrescripcion_SeRegistra()
        {
            var sesion = await Servicio().RegistrarSesionAsync(Request(SocioA, null));

            Assert.Null(sesion.PrescripcionEntrenamientoId);
            Assert.Single(_sesiones.Items);
        }

        [Fact]
        public async Task SinAccesoAlSocio_LanzaAccesoDenegadoAntesDeValidarLaPrescripcion()
        {
            var p = Prescripcion(SocioB, EstadoPrescripcion.Aprobada);
            _acceso.Permitidos.Remove(SocioB);

            await Assert.ThrowsAsync<AccesoDenegadoException>(() => Servicio().RegistrarSesionAsync(Request(SocioB, p.Id)));
            Assert.Empty(_sesiones.Items);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(6)]
        public async Task RirFueraDeRango_LanzaArgumentException(int rir)
        {
            var p = Prescripcion(SocioA, EstadoPrescripcion.Aprobada);

            await Assert.ThrowsAsync<ArgumentException>(() => Servicio().RegistrarSesionAsync(Request(SocioA, p.Id, rir)));
        }

        [Theory]
        [InlineData(EstadoPrescripcion.Aprobada, true)]
        [InlineData(EstadoPrescripcion.Editada, true)]
        [InlineData(EstadoPrescripcion.PendienteRevision, false)]
        [InlineData(EstadoPrescripcion.Rechazada, false)]
        [InlineData(EstadoPrescripcion.Creada, false)]
        public void EsVigente_SoloAprobadaOEditada(EstadoPrescripcion estado, bool esperado)
        {
            Assert.Equal(esperado, new PrescripcionEntrenamiento { Estado = estado }.EsVigente());
        }

        // ---- Etapa 9a-1: ejercicios medidos en segundos ----

        /// <summary>Sesión SINTÉTICA con una serie de calentamiento y dos efectivas del ejercicio dado, vinculada a su prescripción.</summary>
        private SesionEntrenamiento SesionCon(TipoMedida medida, int min, int max, params (decimal Kg, int Valor, bool Calentamiento)[] series)
        {
            var ejercicio = new Ejercicio { Id = EjercicioId, Nombre = "Ejercicio de prueba", TipoMedida = medida };
            var p = Prescripcion(SocioA, EstadoPrescripcion.Aprobada);
            p.EjerciciosPrescritos.Add(new PrescripcionEjercicio
            {
                EjercicioId = EjercicioId, Ejercicio = ejercicio, Orden = 1, SeriesObjetivo = 2,
                RepeticionesMinimas = min, RepeticionesMaximas = max, DescansoObjetivoSegundos = 60,
            });
            var sesion = new SesionEntrenamiento { SocioId = SocioA, PrescripcionEntrenamientoId = p.Id };
            var ej = new SesionEjercicio { EjercicioId = EjercicioId, Ejercicio = ejercicio, Orden = 1 };
            var orden = 0;
            foreach (var (kg, valor, cal) in series)
                ej.SeriesRealizadas.Add(new SerieRealizada { OrdenSerie = ++orden, PesoLevantadoKg = kg, Repeticiones = valor, EsCalentamiento = cal });
            sesion.EjerciciosEjecutados.Add(ej);
            _sesiones.Items.Add(sesion);
            return sesion;
        }

        [Fact]
        public async Task EjercicioEnSegundos_TieneVolumenCero_YElMensajeNoDiceRepeticiones()
        {
            // Plancha con lastre de 10 kg: 15 s de calentamiento, 25 s y 50 s efectivos (rango 20-40 s).
            var sesion = SesionCon(TipoMedida.Segundos, 20, 40, (10m, 15, true), (10m, 25, false), (10m, 50, false));

            var c = await Servicio().GetComparacionAsync(sesion.Id);

            var e = Assert.Single(c!.Ejercicios);
            Assert.Equal("Segundos", e.TipoMedida);
            Assert.Equal(0m, e.Ejecucion!.VolumenTotalKg);
            Assert.Equal(0m, e.Ejecucion.VolumenEfectivoKg);
            Assert.Equal(0m, e.Ejecucion.VolumenCalentamientoKg);
            Assert.All(e.Ejecucion.DetallesSeries, d => Assert.Equal(0m, d.Volumen));

            // La comparación de rango sigue igual; solo cambia la unidad del mensaje.
            var excedida = e.Ejecucion.DetallesSeries.Single(d => d.Repeticiones == 50);
            Assert.False(excedida.RepeticionesComparacion!.CumpleObjetivo);
            Assert.Equal(10, excedida.RepeticionesComparacion.Desviacion);
            Assert.Equal("Excedieron 10 s.", excedida.RepeticionesComparacion.Mensaje);
            Assert.DoesNotContain("rep", excedida.RepeticionesComparacion.Mensaje);
            Assert.True(e.Ejecucion.DetallesSeries.Single(d => d.Repeticiones == 25).RepeticionesComparacion!.CumpleObjetivo);
        }

        [Fact]
        public async Task EjercicioEnRepeticiones_MantieneElVolumenEnKg()
        {
            var sesion = SesionCon(TipoMedida.Repeticiones, 8, 12, (20m, 10, true), (40m, 10, false), (40m, 6, false));

            var c = await Servicio().GetComparacionAsync(sesion.Id);

            var e = Assert.Single(c!.Ejercicios);
            Assert.Equal("Repeticiones", e.TipoMedida);
            Assert.Equal(840m, e.Ejecucion!.VolumenTotalKg);
            Assert.Equal(640m, e.Ejecucion.VolumenEfectivoKg);
            Assert.Equal(200m, e.Ejecucion.VolumenCalentamientoKg);
            Assert.Equal("Faltaron 2 reps.", e.Ejecucion.DetallesSeries.Single(d => d.Repeticiones == 6).RepeticionesComparacion!.Mensaje);
        }

        // ---- Dobles de prueba ----
        private sealed class FakeAcceso : ISocioAccessService
        {
            public HashSet<Guid> Permitidos { get; } = new() { SocioA, SocioB };
            public Task AsegurarAccesoAsync(Guid socioId) =>
                Permitidos.Contains(socioId) ? Task.CompletedTask : throw new AccesoDenegadoException();
        }

        private sealed class FakeSesionRepository : ISesionEntrenamientoRepository
        {
            public List<SesionEntrenamiento> Items { get; } = new();
            public Task<SesionEntrenamiento> AddAsync(SesionEntrenamiento sesion) { Items.Add(sesion); return Task.FromResult(sesion); }
            public Task<SesionEntrenamiento?> GetByIdAsync(Guid id) => Task.FromResult(Items.FirstOrDefault(s => s.Id == id));
            public Task<IEnumerable<SesionEntrenamiento>> GetAllBySocioIdAsync(Guid socioId) =>
                Task.FromResult(Items.Where(s => s.SocioId == socioId));
        }

        private sealed class FakePrescripcionRepository : IPrescripcionEntrenamientoRepository
        {
            public List<PrescripcionEntrenamiento> Items { get; } = new();
            public Task<bool> ExistsAsync(Guid id) => Task.FromResult(Items.Any(p => p.Id == id));
            public Task<PrescripcionEntrenamiento?> GetByIdAsync(Guid id) => Task.FromResult(Items.FirstOrDefault(p => p.Id == id));
            public Task<IEnumerable<PrescripcionEntrenamiento>> GetPendientesDeRevisionAsync() => throw new NotSupportedException();
            public Task<PrescripcionEntrenamiento?> GetForRevisionAsync(Guid id) => throw new NotSupportedException();
            public Task AddRevisionAsync(RevisionPrescripcion revision) => throw new NotSupportedException();
            public Task SaveChangesAsync() => Task.CompletedTask;
        }

        private sealed class FakeSocioRepository : ISocioRepository
        {
            private readonly HashSet<Guid> _ids;
            public FakeSocioRepository(params Guid[] ids) { _ids = ids.ToHashSet(); }
            public Task<bool> ExistsAsync(Guid id) => Task.FromResult(_ids.Contains(id));
            public Task<IEnumerable<Socio>> GetAllAsync() => throw new NotSupportedException();
            public Task<Socio?> GetByIdAsync(Guid id) => throw new NotSupportedException();
            public Task<Socio> AddAsync(Socio socio) => throw new NotSupportedException();
            public Task<IEnumerable<Socio>> GetByEntrenadorAsync(Guid entrenadorUsuarioId) => throw new NotSupportedException();
            public Task<bool> ExisteDniAsync(string dni, Guid? excluirSocioId = null) => throw new NotSupportedException();
            public Task<Socio?> GetForUpdateAsync(Guid id) => throw new NotSupportedException();
            public Task SaveChangesAsync() => Task.CompletedTask;
        }

        private sealed class FakeEjercicioRepository : IEjercicioRepository
        {
            private readonly HashSet<Guid> _ids;
            public FakeEjercicioRepository(params Guid[] ids) { _ids = ids.ToHashSet(); }
            public Task<bool> ExistenTodosAsync(IEnumerable<Guid> ids) => Task.FromResult(ids.All(_ids.Contains));
            public Task<(IReadOnlyList<Ejercicio> Items, int Total)> GetPaginadoAsync(string? texto, string? zonaObjetivo, int omitir, int tomar) =>
                throw new NotSupportedException();
            public Task<Ejercicio?> GetByIdAsync(Guid id) => throw new NotSupportedException();
            public Task<IReadOnlyList<string>> GetZonasObjetivoAsync() => throw new NotSupportedException();
        }
    }
}
