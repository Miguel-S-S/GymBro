using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Domain.Repositories;
using GymBro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GymBro.Infrastructure.Repositories
{
    public class UsuarioRepository : IUsuarioRepository
    {
        private readonly GymBroDbContext _context;
        public UsuarioRepository(GymBroDbContext context) { _context = context; }

        // Login y unicidad de email ocurren antes de conocer el tenant: se ignora el filtro (pero no la baja lógica).
        public Task<Usuario?> GetByEmailAsync(string emailNormalizado) =>
            _context.Usuarios.IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Email == emailNormalizado && u.FechaBaja == null);

        public Task<Usuario?> GetByIdAsync(Guid id) =>
            _context.Usuarios.FirstOrDefaultAsync(u => u.Id == id);

        public async Task<IEnumerable<Usuario>> GetEntrenadoresAsync() =>
            await _context.Usuarios.Where(u => u.Rol == RolUsuario.Entrenador)
                .OrderBy(u => u.NombreCompleto).ToListAsync();

        public async Task AddAsync(Usuario usuario)
        {
            await _context.Usuarios.AddAsync(usuario); // si trae Socio, se inserta en la misma transacción
            await _context.SaveChangesAsync();
        }
    }
}