using GymBro.Application.DTOs.Acceso;
using GymBro.Domain.Entities;

namespace GymBro.Application.Abstractions
{
    public interface IHasheadorContrasena
    {
        string Hash(string contrasena);
        bool Verificar(string hash, string contrasena);
    }

    public interface IGeneradorToken
    {
        LoginResponse Generar(Usuario usuario);
    }
}