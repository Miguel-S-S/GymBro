using GymBro.Domain.Entities;
using GymBro.Domain.Exceptions;
using GymBro.Domain.Repositories;
using GymBro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GymBro.Infrastructure.Repositories
{
    public class RegistroBienestarRepository : IRegistroBienestarRepository
    {
        private readonly GymBroDbContext _context;

        public RegistroBienestarRepository(GymBroDbContext context) { _context = context; }

        public Task<RegistroBienestarDiario?> GetDelDiaAsync(Guid socioId, DateOnly fecha) =>
            _context.RegistrosBienestarDiario.FirstOrDefaultAsync(r => r.SocioId == socioId && r.Fecha == fecha);

        public async Task<IReadOnlyList<RegistroBienestarDiario>> ListarAsync(Guid socioId, DateOnly desde, DateOnly hasta) =>
            await _context.RegistrosBienestarDiario.AsNoTracking()
                .Where(r => r.SocioId == socioId && r.Fecha >= desde && r.Fecha <= hasta)
                .OrderByDescending(r => r.Fecha)
                .ToListAsync();

        public void Agregar(RegistroBienestarDiario registro) => _context.RegistrosBienestarDiario.Add(registro);

        public async Task GuardarCambiosAsync()
        {
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                // Dos envíos simultáneos del mismo día: el índice único (socio_id, fecha) impide el duplicado.
                throw new ReglaNegocioException("El registro de ese día se acaba de guardar desde otro envío. Volvé a abrirlo para editarlo.");
            }
        }
    }
}
