using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GymBro.Domain.Entities;

namespace GymBro.Domain.Repositories
{
    public interface IPrescripcionEntrenamientoRepository
    {
        Task<bool> ExistsAsync(Guid id);
        Task<PrescripcionEntrenamiento?> GetByIdAsync(Guid id);

        /// <summary>Rutinas en PendienteRevision (el filtro de tenant limita al gimnasio actual).</summary>
        Task<IEnumerable<PrescripcionEntrenamiento>> GetPendientesDeRevisionAsync();
        Task<PrescripcionEntrenamiento?> GetForRevisionAsync(Guid id);
        Task AddRevisionAsync(RevisionPrescripcion revision);
        Task SaveChangesAsync();
    }
}