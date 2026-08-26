using GymBro.Application.DTOs;
using GymBro.Application.Services;
using GymBro.Domain.Entities;
using GymBro.Domain.Repositories;

namespace GymBro.Application.Services
{
    public class SocioService : ISocioService
    {
        private readonly ISocioRepository _repository;

        public SocioService(ISocioRepository repository)
        {
            _repository = repository;
        }

        public async Task<IEnumerable<SocioResponseDto>> GetAllSociosAsync()
        {
            var socios = await _repository.GetAllAsync();

            // Mapeo manual (más adelante podemos usar AutoMapper)
            return socios.Select(s => new SocioResponseDto
            {
                Id = s.Id,
                NombreCompleto = $"{s.Nombre} {s.Apellido}",
                Email = s.Email,
                FechaAlta = s.FechaAlta
            });
        }

        public async Task<SocioResponseDto> CreateSocioAsync(SocioCreateDto dto)
        {
            var socio = new Socio
            {
                Nombre = dto.Nombre,
                Apellido = dto.Apellido,
                Email = dto.Email
            };

            var nuevoSocio = await _repository.AddAsync(socio);

            return new SocioResponseDto
            {
                Id = nuevoSocio.Id,
                NombreCompleto = $"{nuevoSocio.Nombre} {nuevoSocio.Apellido}",
                Email = nuevoSocio.Email,
                FechaAlta = nuevoSocio.FechaAlta
            };
        }
    }
}