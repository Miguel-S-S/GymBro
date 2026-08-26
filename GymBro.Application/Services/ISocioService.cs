using GymBro.Application.DTOs;

namespace GymBro.Application.Services
{
    public interface ISocioService
    {
        Task<IEnumerable<SocioResponseDto>> GetAllSociosAsync();
        Task<SocioResponseDto> CreateSocioAsync(SocioCreateDto socioCreateDto);
    }
}
