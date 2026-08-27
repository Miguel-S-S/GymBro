using GymBro.Application.DTOs;
using GymBro.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GymBro.Application.Services
{
    public interface IBuscadorSemanticoService
    {
        Task<List<EjercicioBuscadoDto>> BuscarEjerciciosSimilaresAsync(string requerimientoUsuario, int cantidad = 5);
    }
}