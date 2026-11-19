using GymBro.Domain.Entities;

namespace GymBro.Domain.Repositories
{
    /// <summary>Tipos de equipamiento y equipamiento declarado por cada gimnasio (Etapa 9a-2).</summary>
    public interface IEquipamientoRepository
    {
        /// <summary>Tipos vigentes que aplican al gimnasio: los globales y los propios de ese gimnasio.</summary>
        Task<IReadOnlyList<TipoEquipamiento>> GetTiposParaGimnasioAsync(Guid gimnasioId);

        /// <summary>Tipo vigente y visible para quien llama (filtro de EF), o null.</summary>
        Task<TipoEquipamiento?> GetTipoAsync(Guid id);

        /// <summary>Indica si ya existe la clave en el gimnasio, incluidos los tipos dados de baja.</summary>
        Task<bool> ExisteClavePropiaAsync(Guid gimnasioId, string clave);

        /// <summary>Equipamiento vigente marcado por el gimnasio, con seguimiento.</summary>
        Task<IReadOnlyList<EquipamientoGimnasio>> GetMarcadosAsync(Guid gimnasioId);

        // ---- Etapa 9a-2b: gestión de tipos ----

        /// <summary>Tipo vigente con seguimiento, si es visible para quien llama (filtro de EF), o null.</summary>
        Task<TipoEquipamiento?> GetTipoParaGestionAsync(Guid id);

        /// <summary>Todos los tipos vigentes, globales y de cualquier gimnasio (ignora el filtro; uso interno).</summary>
        Task<IReadOnlyList<TipoEquipamiento>> GetTodosLosTiposVigentesAsync();

        /// <summary>Marcados vigentes de ese tipo en cualquier gimnasio, con seguimiento (ignora el filtro; uso interno).</summary>
        Task<IReadOnlyList<EquipamientoGimnasio>> GetMarcadosDeTipoAsync(Guid tipoEquipamientoId);

        Task AddTipoAsync(TipoEquipamiento tipo);
        Task AddMarcadoAsync(EquipamientoGimnasio marcado);
        Task SaveChangesAsync();
    }
}
