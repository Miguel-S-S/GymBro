using GymBro.Application.DTOs.Bienestar;
using GymBro.Application.Services;
using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Domain.Exceptions;
using GymBro.Domain.Repositories;
using GymBro.Domain.Security;
using Xunit;

namespace GymBro.Tests
{
    /// <summary>
    /// Etapa 8b: servicio de bienestar diario con reloj simulado. Datos sintéticos de prueba.
    /// Buenos Aires es UTC-3 sin horario de verano: 23:30 local del 17/11 = 02:30 UTC del 18/11.
    /// </summary>
    public class BienestarServiceTests
    {
        private static readonly DateTimeOffset Las2330Del17Local = new(2026, 11, 18, 2, 30, 0, TimeSpan.Zero);
        private static readonly DateOnly Dia17 = new(2026, 11, 17);

        private readonly Socio _socio = NuevoSocio(conConsentimiento: true);
        private readonly RegistrosFalsos _registros = new();

        private BienestarService Servicio(Socio socio, RolUsuario rol = RolUsuario.Socio, DateTimeOffset? ahora = null) =>
            new(_registros, new SociosFalsos(socio), new AccesoPermitido(),
                new UsuarioFalso(rol, rol == RolUsuario.Socio ? socio.Id : null), new RelojFijo(ahora ?? Las2330Del17Local));

        private static RegistroBienestarRequest Pedido(Action<RegistroBienestarRequest>? ajuste = null)
        {
            var r = new RegistroBienestarRequest { HorasSueno = 7.5m, Estres = 3, Fatiga = 2 };
            ajuste?.Invoke(r);
            return r;
        }

        // ---- Día local y ventana ----

        [Fact]
        public void A_las_2330_locales_el_dia_es_el_local_y_no_el_UTC_siguiente()
        {
            Assert.Equal(Dia17, DiaLocal.Hoy(new RelojFijo(Las2330Del17Local)));
            Assert.Equal(new DateOnly(2026, 11, 18), DateOnly.FromDateTime(Las2330Del17Local.UtcDateTime)); // el UTC ya es 18
        }

        [Theory]
        [InlineData("2026-11-18T02:59:59Z", "2026-11-17")] // 23:59:59 local
        [InlineData("2026-11-18T03:00:00Z", "2026-11-18")] // 00:00 local
        public void El_dia_local_de_un_instante_UTC_cambia_a_la_medianoche_local(string utc, string esperado) =>
            Assert.Equal(DateOnly.Parse(esperado), DiaLocal.DeUtc(DateTime.Parse(utc).ToUniversalTime()));

        [Fact]
        public async Task A_las_2330_locales_se_guarda_en_el_dia_local()
        {
            var dto = await Servicio(_socio).GuardarAsync(Dia17, Pedido());
            Assert.Equal(Dia17, dto.Fecha);
            Assert.Equal(Dia17, (await Servicio(_socio).ObtenerHoyAsync()).FechaLocal);
        }

        [Theory]
        [InlineData(0, true)]   // hoy
        [InlineData(-1, true)]  // ayer
        [InlineData(-2, false)] // anteayer
        [InlineData(1, false)]  // mañana (aunque en UTC ya sea "hoy")
        public async Task Ventana_de_edicion_hoy_y_ayer(int desplazamiento, bool permitido)
        {
            var guardar = () => Servicio(_socio).GuardarAsync(Dia17.AddDays(desplazamiento), Pedido());
            if (permitido) Assert.NotNull(await guardar());
            else await Assert.ThrowsAsync<ReglaNegocioException>(guardar);
        }

        // ---- Consentimiento (Decisión C) ----

        [Fact]
        public async Task Sin_consentimiento_no_se_guarda()
        {
            var sinConsentimiento = NuevoSocio(conConsentimiento: false);
            await Assert.ThrowsAsync<ReglaNegocioException>(() => Servicio(sinConsentimiento).GuardarAsync(Dia17, Pedido()));
            Assert.Empty(_registros.Todos);
        }

        [Fact]
        public async Task Sin_consentimiento_el_socio_ve_hoy_sin_registro_y_un_historial_vacio()
        {
            var sinConsentimiento = NuevoSocio(conConsentimiento: false);
            var hoy = await Servicio(sinConsentimiento).ObtenerHoyAsync();
            Assert.Equal(Dia17, hoy.FechaLocal);
            Assert.Null(hoy.Registro);
            Assert.Empty(await Servicio(sinConsentimiento).ListarAsync(sinConsentimiento.Id, null, null));
        }

        [Theory]
        [InlineData(RolUsuario.Entrenador)]
        [InlineData(RolUsuario.AdministradorGimnasio)]
        public async Task Sin_consentimiento_el_personal_recibe_400(RolUsuario rol)
        {
            var sinConsentimiento = NuevoSocio(conConsentimiento: false);
            await Assert.ThrowsAsync<ReglaNegocioException>(() =>
                Servicio(sinConsentimiento, rol).ListarAsync(sinConsentimiento.Id, null, null));
        }

        // ---- Alta y actualización ----

        [Fact]
        public async Task Dos_guardados_del_mismo_dia_actualizan_y_no_duplican()
        {
            var primero = await Servicio(_socio).GuardarAsync(Dia17, Pedido());
            var segundo = await Servicio(_socio, ahora: Las2330Del17Local.AddMinutes(10))
                .GuardarAsync(Dia17, Pedido(r => { r.Estres = 6; r.Fatiga = null; }));

            Assert.Single(_registros.Todos);
            Assert.Equal(primero.Id, segundo.Id);
            Assert.Equal((short)6, segundo.Estres);
            Assert.Null(segundo.Fatiga); // el PUT reemplaza el registro completo
            Assert.NotNull(segundo.FechaModificacion);
        }

        [Fact]
        public async Task Solo_el_socio_puede_guardar()
        {
            await Assert.ThrowsAsync<AccesoDenegadoException>(() =>
                Servicio(_socio, RolUsuario.Entrenador).GuardarAsync(Dia17, Pedido()));
        }

        // ---- Rangos y "al menos un dato" ----

        public static TheoryData<string, Action<RegistroBienestarRequest>> Invalidos() => new()
        {
            { "estrés 0", r => r.Estres = 0 },
            { "estrés 8", r => r.Estres = 8 },
            { "fatiga 0", r => r.Fatiga = 0 },
            { "fatiga 8", r => r.Fatiga = 8 },
            { "sueño negativo", r => r.HorasSueno = -0.5m },
            { "sueño 24,5", r => r.HorasSueno = 24.5m },
            { "sueño sin paso de 0,5", r => r.HorasSueno = 7.25m },
            { "peso 0,5", r => r.PesoKg = 0.5m },
            { "peso 400,5", r => r.PesoKg = 400.5m },
            { "peso con dos decimales", r => r.PesoKg = 70.25m },
            { "agua 10,5", r => r.IngestaAguaLitros = 10.5m },
            { "agua negativa", r => r.IngestaAguaLitros = -1m },
            { "FC 29", r => r.FrecuenciaCardiacaReposo = 29 },
            { "FC 121", r => r.FrecuenciaCardiacaReposo = 121 },
            { "ingesta inexistente", r => r.NutricionCategoria = "Media" },
            { "ingesta numérica", r => r.NutricionCategoria = "3" },
            { "nota de 501 caracteres", r => r.NutricionNotas = new string('x', 501) },
        };

        [Theory]
        [MemberData(nameof(Invalidos))]
        public void Rechaza_valores_fuera_de_rango(string caso, Action<RegistroBienestarRequest> ajuste)
        {
            Assert.NotNull(caso);
            Assert.Throws<ReglaNegocioException>(() => BienestarService.Validar(Pedido(ajuste)));
        }

        [Theory]
        [InlineData((short)1)]
        [InlineData((short)7)]
        public void Acepta_los_extremos_de_la_escala_1_a_7(short valor)
        {
            var v = BienestarService.Validar(new RegistroBienestarRequest { Estres = valor, Fatiga = valor });
            Assert.Equal(valor, v.Estres);
            Assert.Equal(valor, v.Fatiga);
        }

        [Fact]
        public void Acepta_los_extremos_de_los_demas_rangos_y_normaliza_la_ingesta()
        {
            var v = BienestarService.Validar(new RegistroBienestarRequest
            {
                HorasSueno = 24m, PesoKg = 400m, IngestaAguaLitros = 0m, FrecuenciaCardiacaReposo = 30,
                NutricionCategoria = " alta ", NutricionNotas = new string('x', 500)
            });
            Assert.Equal(NutricionCategoria.Alta, v.NutricionCategoria);
        }

        [Fact]
        public void Exige_al_menos_un_dato()
        {
            Assert.Throws<ReglaNegocioException>(() => BienestarService.Validar(new RegistroBienestarRequest()));
            Assert.Throws<ReglaNegocioException>(() => BienestarService.Validar(new RegistroBienestarRequest { NutricionNotas = "   " }));
            Assert.NotNull(BienestarService.Validar(new RegistroBienestarRequest { NutricionNotas = "Cena tarde" }));
        }

        // ---- Historial ----

        [Fact]
        public async Task Historial_valida_el_rango_de_fechas()
        {
            await Assert.ThrowsAsync<ReglaNegocioException>(() => Servicio(_socio).ListarAsync(_socio.Id, Dia17, Dia17.AddDays(-1)));
            await Assert.ThrowsAsync<ReglaNegocioException>(() => Servicio(_socio).ListarAsync(_socio.Id, Dia17.AddDays(-366), Dia17));
            Assert.Empty(await Servicio(_socio).ListarAsync(_socio.Id, Dia17.AddDays(-365), Dia17));
        }

        [Fact]
        public async Task Historial_por_defecto_son_los_ultimos_30_dias()
        {
            await Servicio(_socio).GuardarAsync(Dia17, Pedido());
            _registros.Todos.Add(new RegistroBienestarDiario { SocioId = _socio.Id, Fecha = Dia17.AddDays(-30), Estres = 1 });

            var lista = await Servicio(_socio).ListarAsync(_socio.Id, null, null);
            Assert.Equal(Dia17, Assert.Single(lista).Fecha);
        }

        // ---- Fakes ----

        private static Socio NuevoSocio(bool conConsentimiento) => new()
        {
            Id = Guid.NewGuid(), Nombre = "Prueba", Apellido = "Sintética", Email = $"{Guid.NewGuid():N}@test.local",
            ConsentimientoDatosSaludFecha = conConsentimiento ? new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc) : null,
        };

        private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => ahora;
        }

        private sealed class UsuarioFalso(RolUsuario rol, Guid? socioId) : IUsuarioActual
        {
            public bool EsAutenticado => true;
            public bool EsProcesoDeSistema => false;
            public Guid? UsuarioId { get; } = Guid.NewGuid();
            public RolUsuario? Rol => rol;
            public Guid? GimnasioId => null;
            public Guid? SocioId => socioId;
        }

        private sealed class AccesoPermitido : ISocioAccessService
        {
            public Task AsegurarAccesoAsync(Guid socioId) => Task.CompletedTask;
        }

        private sealed class RegistrosFalsos : IRegistroBienestarRepository
        {
            public List<RegistroBienestarDiario> Todos { get; } = new();
            public Task<RegistroBienestarDiario?> GetDelDiaAsync(Guid socioId, DateOnly fecha) =>
                Task.FromResult(Todos.FirstOrDefault(r => r.SocioId == socioId && r.Fecha == fecha));
            public Task<IReadOnlyList<RegistroBienestarDiario>> ListarAsync(Guid socioId, DateOnly desde, DateOnly hasta) =>
                Task.FromResult<IReadOnlyList<RegistroBienestarDiario>>(
                    Todos.Where(r => r.SocioId == socioId && r.Fecha >= desde && r.Fecha <= hasta).OrderByDescending(r => r.Fecha).ToList());
            public void Agregar(RegistroBienestarDiario registro) => Todos.Add(registro);
            public Task GuardarCambiosAsync() => Task.CompletedTask;
        }

        private sealed class SociosFalsos(Socio socio) : ISocioRepository
        {
            public Task<Socio?> GetByIdAsync(Guid id) => Task.FromResult(id == socio.Id ? socio : null);
            public Task<bool> ExistsAsync(Guid id) => Task.FromResult(id == socio.Id);
            public Task<IEnumerable<Socio>> GetAllAsync() => throw new NotImplementedException();
            public Task<Socio> AddAsync(Socio s) => throw new NotImplementedException();
            public Task<IEnumerable<Socio>> GetByEntrenadorAsync(Guid entrenadorUsuarioId) => throw new NotImplementedException();
            public Task<bool> ExisteDniAsync(string dni, Guid? excluirSocioId = null) => throw new NotImplementedException();
            public Task<Socio?> GetForUpdateAsync(Guid id) => throw new NotImplementedException();
            public Task SaveChangesAsync() => throw new NotImplementedException();
        }
    }
}
