using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GymBro.Application.DTOs;
using GymBro.Application.DTOs.Comparacion;

namespace GymBro.Application.Services
{
    public interface ISesionEntrenamientoService
    {
        Task<SesionEntrenamientoResponseDto> RegistrarSesionAsync(RegistrarSesionEntrenamientoRequest request);
        Task<SesionEntrenamientoResponseDto?> GetByIdAsync(Guid id);
        Task<IEnumerable<SesionEntrenamientoResponseDto>> GetAllBySocioIdAsync(Guid socioId);
        Task<ComparacionSesionResponseDto?> GetComparacionAsync(Guid sesionId);
    }
}
