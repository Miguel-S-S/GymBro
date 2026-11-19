using GymBro.Application.DTOs;
using GymBro.Domain.Repositories;
using GymBro.Application.Services;
using GymBro.Domain.Enums;

namespace GymBro.Application.Services
{
    public class EjercicioCatalogoService : IEjercicioCatalogoService
    {
        public const int TamanioPaginaMaximo = 100;

        private readonly IEjercicioRepository _repository;

        public EjercicioCatalogoService(IEjercicioRepository repository)
        {
            _repository = repository;
        }

        public async Task<PaginadoDto<EjercicioResumenDto>> GetCatalogoAsync(
            string? texto, string? zonaObjetivo, int pagina, int tamanioPagina, string? origen = null)
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

            var (items, total) = await _repository.GetPaginadoAsync(
                texto, zonaObjetivo, (pagina - 1) * tamanioPagina, tamanioPagina, filtroOrigen);

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
                    TipoEquipamiento = e.TipoEquipamiento?.Nombre
                }).ToList(),
                Total = total,
                Pagina = pagina,
                TamanioPagina = tamanioPagina
            };
        }

        public async Task<EjercicioDetalleDto?> GetByIdAsync(Guid id)
        {
            var e = await _repository.GetByIdAsync(id);
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
                TipoEquipamiento = e.TipoEquipamiento?.Nombre
            };
        }

        public Task<IReadOnlyList<string>> GetZonasObjetivoAsync()
        {
            return _repository.GetZonasObjetivoAsync();
        }
    }
}
