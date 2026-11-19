using GymBro.Application.DTOs;
using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Domain.Exceptions;
using GymBro.Domain.Repositories;
using GymBro.Domain.Security;

namespace GymBro.Application.Services
{
    public class EjercicioCatalogoService : IEjercicioCatalogoService
    {
        public const int TamanioPaginaMaximo = 100;

        private readonly IEjercicioRepository _repository;
        private readonly IUsuarioActual _actual;

        public EjercicioCatalogoService(IEjercicioRepository repository, IUsuarioActual actual)
        {
            _repository = repository;
            _actual = actual;
        }

        /// <summary>
        /// Etapa 9a-2b: ejercicios desactivados que puede ver quien llama. Administrador de la Plataforma: todos.
        /// Dueño y Personal Trainer: los propios de su gimnasio. Socio u otro: ninguno (null).
        /// </summary>
        public static AlcanceInactivos? AlcanceDe(IUsuarioActual actual) => actual.Rol switch
        {
            RolUsuario.SuperAdministrador => new AlcanceInactivos(true, null),
            RolUsuario.AdministradorGimnasio or RolUsuario.Entrenador when actual.GimnasioId is not null
                => new AlcanceInactivos(false, actual.GimnasioId),
            _ => null
        };

        /// <summary>
        /// Etapa 9a-2b: qué puede hacer quien llama con un ejercicio que ya ve. Es la misma regla que aplica
        /// EjercicioGimnasioService al modificar (el servidor la vuelve a validar); acá solo informa a la interfaz.
        ///   - Global: nadie lo edita por la API (script); solo la plataforma lo desactiva o reactiva.
        ///   - Propio: la plataforma (cualquiera), el Dueño (los de su gimnasio) y el Personal Trainer (los que él creó).
        /// </summary>
        public static (bool Editar, bool CambiarEstado) Permisos(IUsuarioActual actual, Ejercicio e)
        {
            if (e.GimnasioId is null) return (false, actual.Rol == RolUsuario.SuperAdministrador);
            var puede = actual.Rol switch
            {
                RolUsuario.SuperAdministrador => true,
                RolUsuario.AdministradorGimnasio => e.GimnasioId == actual.GimnasioId,
                RolUsuario.Entrenador => e.GimnasioId == actual.GimnasioId && e.CreadoPorUsuarioId != null
                                         && e.CreadoPorUsuarioId == actual.UsuarioId,
                _ => false
            };
            return (puede, puede);
        }

        public async Task<PaginadoDto<EjercicioResumenDto>> GetCatalogoAsync(
            string? texto, string? zonaObjetivo, int pagina, int tamanioPagina, string? origen = null, bool inactivos = false)
        {
            if (pagina < 1)
                throw new ArgumentException("La página debe ser mayor o igual a 1.");
            if (tamanioPagina < 1 || tamanioPagina > TamanioPaginaMaximo)
                throw new ArgumentException($"El tamaño de página debe estar entre 1 y {TamanioPaginaMaximo}.");

            OrigenEjercicio? filtroOrigen = null;
            if (!string.IsNullOrWhiteSpace(origen))
            {
                if (!Enum.TryParse<OrigenEjercicio>(origen.Trim(), true, out var o) || !Enum.IsDefined(o) || int.TryParse(origen, out _))
                    throw new ArgumentException("El origen debe ser Dataset, Proyecto o Gimnasio.");
                filtroOrigen = o;
            }

            AlcanceInactivos? alcance = null;
            if (inactivos)
                alcance = AlcanceDe(_actual) ?? throw new AccesoDenegadoException("Solo quien gestiona el catálogo ve los ejercicios desactivados.");

            var (items, total) = await _repository.GetPaginadoAsync(
                texto, zonaObjetivo, (pagina - 1) * tamanioPagina, tamanioPagina, filtroOrigen, alcance);

            return new PaginadoDto<EjercicioResumenDto>
            {
                Items = items.Select(e => new EjercicioResumenDto
                {
                    Id = e.Id,
                    Nombre = e.Nombre,
                    ZonaObjetivo = e.ZonaObjetivo,
                    MusculoPrincipal = e.MusculoPrincipal,
                    Equipamiento = e.Equipamiento,
                    Dificultad = e.Dificultad,
                    TipoMedida = e.TipoMedida.ToString(),
                    Origen = e.Origen.ToString(),
                    GimnasioId = e.GimnasioId,
                    TipoEquipamientoId = e.TipoEquipamientoId,
                    TipoEquipamiento = e.TipoEquipamiento?.Nombre,
                    Activo = e.Activo,
                    CreadoPorUsuarioId = e.CreadoPorUsuarioId,
                    CreadoPor = NombreAutor(e),
                    PuedeEditar = Permisos(_actual, e).Editar,
                    PuedeCambiarEstado = Permisos(_actual, e).CambiarEstado
                }).ToList(),
                Total = total,
                Pagina = pagina,
                TamanioPagina = tamanioPagina
            };
        }

        /// <summary>El activo visible o, para quien gestiona el catálogo, también el desactivado de su alcance.</summary>
        public async Task<EjercicioDetalleDto?> GetByIdAsync(Guid id)
        {
            var e = await _repository.GetByIdAsync(id, AlcanceDe(_actual));
            if (e == null) return null;

            return new EjercicioDetalleDto
            {
                Id = e.Id,
                Nombre = e.Nombre,
                Equipamiento = e.Equipamiento,
                ZonaObjetivo = e.ZonaObjetivo,
                MusculoPrincipal = e.MusculoPrincipal,
                Sinergistas = e.Sinergistas,
                Estabilizadores = e.Estabilizadores,
                Preparacion = e.Preparacion,
                Ejecucion = e.Ejecucion,
                Dificultad = e.Dificultad,
                TipoMedida = e.TipoMedida.ToString(),
                Origen = e.Origen.ToString(),
                GimnasioId = e.GimnasioId,
                TipoEquipamientoId = e.TipoEquipamientoId,
                TipoEquipamiento = e.TipoEquipamiento?.Nombre,
                Activo = e.Activo,
                CreadoPorUsuarioId = e.CreadoPorUsuarioId,
                CreadoPor = NombreAutor(e),
                PuedeEditar = Permisos(_actual, e).Editar,
                PuedeCambiarEstado = Permisos(_actual, e).CambiarEstado
            };
        }

        public Task<IReadOnlyList<string>> GetZonasObjetivoAsync()
        {
            return _repository.GetZonasObjetivoAsync();
        }

        /// <summary>
        /// "Propuesto por". El Socio no lo ve (dato del personal, no del catálogo). Si el autor es el Administrador de la
        /// Plataforma, el personal del gimnasio ve el rol y no el nombre; el Administrador ve siempre el nombre.
        /// </summary>
        private string? NombreAutor(Ejercicio e)
        {
            if (_actual.Rol == RolUsuario.Socio || e.CreadoPor is null) return null;
            if (e.CreadoPor.Rol == RolUsuario.SuperAdministrador && _actual.Rol != RolUsuario.SuperAdministrador)
                return "Administrador de la Plataforma";
            return e.CreadoPor.NombreCompleto;
        }
    }
}
