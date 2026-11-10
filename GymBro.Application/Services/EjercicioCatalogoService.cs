using GymBro.Application.DTOs;
using GymBro.Domain.Repositories;
using GymBro.Application.Services;

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
            string? texto, string? zonaObjetivo, int pagina, int tamanioPagina)
        {
            if (pagina < 1)
                throw new ArgumentException("La página debe ser mayor o igual a 1.");
            if (tamanioPagina < 1 || tamanioPagina > TamanioPaginaMaximo)
                throw new ArgumentException($"El tamaño de página debe estar entre 1 y {TamanioPaginaMaximo}.");

            var (items, total) = await _repository.GetPaginadoAsync(
                texto, zonaObjetivo, (pagina - 1) * tamanioPagina, tamanioPagina);

            return new PaginadoDto<EjercicioResumenDto>
            {
                Items = items.Select(e => new EjercicioResumenDto
                {
                    Id = e.Id,
                    Nombre = e.Nombre,
                    ZonaObjetivo = e.ZonaObjetivo,
                    MusculoPrincipal = e.MusculoPrincipal,
                    Equipamiento = e.Equipamiento,
                    Dificultad = e.Dificultad
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
                Dificultad = e.Dificultad
            };
        }

        public Task<IReadOnlyList<string>> GetZonasObjetivoAsync()
        {
            return _repository.GetZonasObjetivoAsync();
        }
    }
}
