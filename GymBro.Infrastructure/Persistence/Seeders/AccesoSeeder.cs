using GymBro.Application.Abstractions;
using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GymBro.Infrastructure.Persistence.Seeders
{
    public static class AccesoSeeder
    {
        /// <summary>Crea la cuenta del Administrador de la Plataforma si todavía no existe ninguna.</summary>
        public static async Task SeedSuperAdministradorAsync(GymBroDbContext context, IHasheadorContrasena hasheador,
            string? email, string? password)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                Console.WriteLine("[ACCESO] SuperAdmin:Email / SuperAdmin:Password no configurados: no se crea el administrador de la plataforma.");
                return;
            }

            if (await context.Usuarios.AnyAsync(u => u.Rol == RolUsuario.SuperAdministrador)) return;

            context.Usuarios.Add(new Usuario
            {
                Email = email.Trim().ToLowerInvariant(),
                PasswordHash = hasheador.Hash(password),
                NombreCompleto = "Administrador de la Plataforma",
                Rol = RolUsuario.SuperAdministrador
            });
            await context.SaveChangesAsync();
            Console.WriteLine("[ACCESO] Administrador de la plataforma creado.");
        }
    }
}