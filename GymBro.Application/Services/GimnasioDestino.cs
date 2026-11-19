using GymBro.Domain.Enums;
using GymBro.Domain.Exceptions;
using GymBro.Domain.Repositories;
using GymBro.Domain.Security;

namespace GymBro.Application.Services
{
    /// <summary>
    /// Etapa 9a-2b: gimnasio sobre el que se escribe el catálogo o el equipamiento.
    ///   - Administrador de la Plataforma: no tiene gimnasio en el JWT, así que debe indicarlo (400 si falta) y se valida
    ///     que exista (404 si no).
    ///   - Dueño y Personal Trainer: el de su JWT. Si el pedido indica otro gimnasio se responde 404, igual que un recurso
    ///     no visible: el parámetro no sirve para saltarse el aislamiento entre gimnasios.
    /// </summary>
    public static class GimnasioDestino
    {
        public static async Task<Guid> ResolverAsync(IUsuarioActual actual, IGimnasioRepository gimnasios, Guid? indicado)
        {
            if (actual.Rol == RolUsuario.SuperAdministrador)
            {
                var id = indicado ?? throw new ReglaNegocioException("Indique el gimnasio.");
                _ = await gimnasios.GetByIdAsync(id) ?? throw new RecursoNoEncontradoException("Gimnasio no encontrado.");
                return id;
            }

            var propio = actual.GimnasioId ?? throw new AccesoDenegadoException();
            if (indicado is not null && indicado != propio)
                throw new RecursoNoEncontradoException("Gimnasio no encontrado.");
            return propio;
        }
    }
}
