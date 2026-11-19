using GymBro.Domain.Entities;
using GymBro.Domain.Repositories;
using GymBro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GymBro.Infrastructure.Repositories
{
    public class EquipamientoRepository : IEquipamientoRepository
    {
        private readonly GymBroDbContext _context;
        public EquipamientoRepository(GymBroDbContext context) { _context = context; }

        public async Task<IReadOnlyList<TipoEquipamiento>> GetTiposParaGimnasioAsync(Guid gimnasioId) =>
            await _context.TiposEquipamiento.AsNoTracking()
                .Where(t => t.GimnasioId == null || t.GimnasioId == gimnasioId)
                .ToListAsync();

        public Task<TipoEquipamiento?> GetTipoAsync(Guid id) =>
            _context.TiposEquipamiento.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);

        public Task<bool> ExisteClavePropiaAsync(Guid gimnasioId, string clave) =>
            _context.TiposEquipamiento.IgnoreQueryFilters().AnyAsync(t => t.GimnasioId == gimnasioId && t.Clave == clave);

        public async Task<IReadOnlyList<EquipamientoGimnasio>> GetMarcadosAsync(Guid gimnasioId) =>
            await _context.EquipamientosGimnasio.Where(x => x.GimnasioId == gimnasioId).ToListAsync();

        public Task<TipoEquipamiento?> GetTipoParaGestionAsync(Guid id) =>
            _context.TiposEquipamiento.FirstOrDefaultAsync(t => t.Id == id);

        public async Task<IReadOnlyList<TipoEquipamiento>> GetTodosLosTiposVigentesAsync() =>
            await _context.TiposEquipamiento.IgnoreQueryFilters().AsNoTracking().Where(t => t.FechaBaja == null).ToListAsync();

        public async Task<IReadOnlyList<EquipamientoGimnasio>> GetMarcadosDeTipoAsync(Guid tipoEquipamientoId) =>
            await _context.EquipamientosGimnasio.IgnoreQueryFilters()
                .Where(x => x.TipoEquipamientoId == tipoEquipamientoId && x.FechaBaja == null).ToListAsync();

        public async Task AddTipoAsync(TipoEquipamiento tipo) => await _context.TiposEquipamiento.AddAsync(tipo);

        public async Task AddMarcadoAsync(EquipamientoGimnasio marcado) => await _context.EquipamientosGimnasio.AddAsync(marcado);

        public Task SaveChangesAsync() => _context.SaveChangesAsync();
    }
}
