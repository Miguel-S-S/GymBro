using GymBro.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace GymBro.Domain.Repositories
{
    public interface IMedicionRepository
    {
        Task<MedicionFisica> AddAsync(MedicionFisica medicion);
        Task<IEnumerable<MedicionFisica>> GetBySocioIdAsync(Guid socioId);
    }
}
