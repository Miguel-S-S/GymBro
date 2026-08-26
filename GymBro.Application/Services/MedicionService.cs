using GymBro.Domain.Entities;
using GymBro.Domain.Repositories;
using GymBro.Application.DTOs;

namespace GymBro.Application.Services
{
    public class MedicionService: IMedicionService
    {
        private readonly IMedicionRepository _repository;

        public MedicionService(IMedicionRepository repository)
        {
            _repository = repository;
        }

        public async Task RegistrarMedicionAsync(MedicionCreateDto dto)
        {
            var medicion = new MedicionFisica
            {
                SocioId = dto.SocioId,     
                PesoKg = dto.PesoKg,
                AlturaCm = dto.AlturaCm               
            };
            await _repository.AddAsync(medicion);
        }
    }
}
