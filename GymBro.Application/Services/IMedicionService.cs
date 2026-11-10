using GymBro.Application.DTOs;

namespace GymBro.Application.Services
{
    public interface IMedicionService
    {
        Task RegistrarMedicionAsync(MedicionCreateDto dto);
        Task<IEnumerable<MedicionResponseDto>> GetBySocioIdAsync(Guid socioId);
    }

}