using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GymBro.Domain.Entities;

namespace GymBro.Domain.Repositories
{
    public interface ISesionEntrenamientoRepository
    {
        Task<SesionEntrenamiento> AddAsync(SesionEntrenamiento sesion);
        Task<SesionEntrenamiento?> GetByIdAsync(Guid id);
        Task<IEnumerable<SesionEntrenamiento>> GetAllBySocioIdAsync(Guid socioId);
    }
}
