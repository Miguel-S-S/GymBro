using System;
using System.Threading.Tasks;
using GymBro.Domain.Entities;

namespace GymBro.Domain.Repositories
{
    public interface IPrescripcionEntrenamientoRepository
    {
        Task<bool> ExistsAsync(Guid id);
        Task<PrescripcionEntrenamiento?> GetByIdAsync(Guid id);
    }
}
