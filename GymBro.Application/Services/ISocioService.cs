using GymBro.Application.DTOs;

namespace GymBro.Application.Services
{
    public interface ISocioService
    {
        Task<IEnumerable<SocioResponseDto>> GetAllSociosAsync();
        Task<SocioDetalleDto?> GetSocioByIdAsync(Guid id);

        /// <summary>Alta presencial del Dueño: crea la ficha y emite la invitación para que el socio active su cuenta.</summary>
        Task<SocioAltaResponseDto> CreateSocioAsync(SocioCreateDto socioCreateDto);

        /// <summary>Corrige la ficha mientras el socio no haya activado su cuenta.</summary>
        Task<SocioDetalleDto> ActualizarSocioAsync(Guid id, SocioUpdateDto dto);

        /// <summary>Emite una invitación nueva (la anterior deja de valer). Solo mientras el socio no tenga cuenta.</summary>
        Task<InvitacionResponseDto> RenovarInvitacionAsync(Guid id);

        /// <summary>Ficha sin cuenta: baja lógica. Socio con cuenta: se desvincula del gimnasio y sigue como independiente.</summary>
        Task BajaSocioAsync(Guid id);

        /// <summary>
        /// Etapa 9a-2b: cambia el estado administrativo (Activo o Inactivo) de la ficha. Solo el Dueño, sobre su gimnasio.
        /// Es informativo: no bloquea el acceso, las sesiones ni las rutinas.
        /// </summary>
        Task<SocioDetalleDto> CambiarEstadoAsync(Guid id, string estado);
    }
}
