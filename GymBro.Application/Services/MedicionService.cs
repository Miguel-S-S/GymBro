using GymBro.Domain.Entities;
using GymBro.Domain.Repositories;
using GymBro.Application.DTOs;
using GymBro.Application.Abstractions;

namespace GymBro.Application.Services
{
    public class MedicionService : IMedicionService
    {
        private readonly IMedicionRepository _repository;
        private readonly ISocioAccessService _acceso;

        public MedicionService(IMedicionRepository repository, ISocioAccessService acceso)
        {
            _repository = repository;
            _acceso = acceso;
        }

        public async Task RegistrarMedicionAsync(MedicionCreateDto dto)
        {
            await _acceso.AsegurarAccesoAsync(dto.SocioId);
            var medicion = new MedicionFisica
            {
                SocioId = dto.SocioId,
                PesoKg = dto.PesoKg,
                AlturaCm = dto.AlturaCm
            };
            await _repository.AddAsync(medicion);
        }

        public async Task<IEnumerable<MedicionResponseDto>> GetBySocioIdAsync(Guid socioId)
        {
            await _acceso.AsegurarAccesoAsync(socioId);
            var mediciones = await _repository.GetBySocioIdAsync(socioId);
            return mediciones.Select(MapToDto);
        }

        private static MedicionResponseDto MapToDto(MedicionFisica m) => new()
        {
            Id = m.Id,
            SocioId = m.SocioId,
            FechaAlta = m.FechaAlta,

            PesoKg = m.PesoKg,
            AlturaCm = m.AlturaCm,
            PorcentajeGrasa = m.PorcentajeGrasa,
            IMC = m.IMC,

            FrecuenciaEntrenamientoDias = m.FrecuenciaEntrenamientoDias,
            TipoEntrenamiento = m.TipoEntrenamiento.ToString(),
            IngestaAguaLitros = m.IngestaAguaLitros,

            FrecuenciaCardiacaReposo = m.FrecuenciaCardiacaReposo,
            PresionArterialSistolica = m.PresionArterialSistolica,
            PresionArterialDiastolica = m.PresionArterialDiastolica,
            PresionArterialMedia = m.PresionArterialMedia,

            CondicionMedica = m.CondicionMedica,
            NivelEstres = m.NivelEstres,
            HorasSuenoUltimaNoche = m.HorasSuenoUltimaNoche
        };
    }
}