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
    /// Etapa 8c, T7: 404 si el socio no es visible para quien llama; 403 solo si el rol no puede actuar.
    /// Datos sintéticos de prueba.
    /// </summary>
    public class SocioAccessServiceTests
    {
        private static readonly Guid SocioPropio = Guid.NewGuid();
        private static readonly Guid SocioAjeno = Guid.NewGuid();
        private static readonly Guid Trainer = Guid.NewGuid();

        private static SocioAccessService Servicio(UsuarioFalso actual) =>
            new(actual, new SociosFalsos(SocioPropio), new AsignacionesFalsas(Trainer, SocioPropio));

        [Fact]
        public async Task Socio_ve_lo_suyo() =>
            await Servicio(new(RolUsuario.Socio, socioId: SocioPropio)).AsegurarAccesoAsync(SocioPropio);

        [Fact]
        public async Task Socio_sobre_otro_socio_da_404() =>
            await Assert.ThrowsAsync<RecursoNoEncontradoException>(() =>
                Servicio(new(RolUsuario.Socio, socioId: SocioPropio)).AsegurarAccesoAsync(SocioAjeno));

        [Fact]
        public async Task Trainer_ve_a_su_asignado() =>
            await Servicio(new(RolUsuario.Entrenador, usuarioId: Trainer)).AsegurarAccesoAsync(SocioPropio);

        [Fact]
        public async Task Trainer_sobre_socio_no_asignado_da_404() =>
            await Assert.ThrowsAsync<RecursoNoEncontradoException>(() =>
                Servicio(new(RolUsuario.Entrenador, usuarioId: Trainer)).AsegurarAccesoAsync(SocioAjeno));

        [Fact]
        public async Task Dueno_ve_socios_visibles_por_el_tenant() =>
            await Servicio(new(RolUsuario.AdministradorGimnasio)).AsegurarAccesoAsync(SocioPropio);

        [Fact]
        public async Task Dueno_sobre_socio_de_otro_gimnasio_da_404() =>
            await Assert.ThrowsAsync<RecursoNoEncontradoException>(() =>
                Servicio(new(RolUsuario.AdministradorGimnasio)).AsegurarAccesoAsync(SocioAjeno));

        [Fact]
        public async Task Administrador_de_la_Plataforma_da_403_aun_si_el_socio_existe() =>
            await Assert.ThrowsAsync<AccesoDenegadoException>(() =>
                Servicio(new(RolUsuario.SuperAdministrador)).AsegurarAccesoAsync(SocioPropio));

        [Fact]
        public async Task Sin_autenticar_da_403() =>
            await Assert.ThrowsAsync<AccesoDenegadoException>(() =>
                Servicio(new(null, autenticado: false)).AsegurarAccesoAsync(SocioPropio));

        private sealed class UsuarioFalso(RolUsuario? rol, Guid? usuarioId = null, Guid? socioId = null, bool autenticado = true) : IUsuarioActual
        {
            public bool EsAutenticado => autenticado;
            public bool EsProcesoDeSistema => false;
            public Guid? UsuarioId => usuarioId ?? Guid.NewGuid();
            public RolUsuario? Rol => rol;
            public Guid? GimnasioId => null;
            public Guid? SocioId => socioId;
        }

        /// <summary>Simula el filtro de tenant: solo `visible` existe para quien consulta.</summary>
        private sealed class SociosFalsos(Guid visible) : ISocioRepository
        {
            public Task<bool> ExistsAsync(Guid id) => Task.FromResult(id == visible);
            public Task<IEnumerable<Socio>> GetAllAsync() => throw new NotImplementedException();
            public Task<Socio?> GetByIdAsync(Guid id) => throw new NotImplementedException();
            public Task<Socio> AddAsync(Socio socio) => throw new NotImplementedException();
            public Task<IEnumerable<Socio>> GetByEntrenadorAsync(Guid entrenadorUsuarioId) => throw new NotImplementedException();
            public Task<bool> ExisteDniAsync(string dni, Guid? excluirSocioId = null) => throw new NotImplementedException();
            public Task<Socio?> GetForUpdateAsync(Guid id) => throw new NotImplementedException();
            public Task SaveChangesAsync() => throw new NotImplementedException();
        }

        private sealed class AsignacionesFalsas(Guid trainer, Guid socio) : IAsignacionRepository
        {
            public Task<bool> EstaAsignadoAsync(Guid entrenadorUsuarioId, Guid socioId) =>
                Task.FromResult(entrenadorUsuarioId == trainer && socioId == socio);
            public Task<IEnumerable<Guid>> GetSocioIdsAsync(Guid entrenadorUsuarioId) => throw new NotImplementedException();
            public Task AddAsync(AsignacionEntrenadorSocio asignacion) => throw new NotImplementedException();
            public Task<bool> ExisteAsync(Guid entrenadorUsuarioId, Guid socioId) => throw new NotImplementedException();
            public Task QuitarDeSocioAsync(Guid socioId) => throw new NotImplementedException();
        }
    }
}
