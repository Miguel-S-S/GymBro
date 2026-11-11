using GymBro.Domain.Enums;
using GymBro.Domain.Exceptions;

namespace GymBro.Domain.Entities
{
    /// <summary>Pedido de un socio para pertenecer a un gimnasio, validado por DNI por el Dueño.</summary>
    public class SolicitudVinculacion : EntidadAuditable
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid SocioId { get; set; }
        public Socio Socio { get; set; } = null!;
        public Guid GimnasioId { get; set; }
        public Gimnasio Gimnasio { get; set; } = null!;

        /// <summary>Copia del nombre al momento de pedir: el Dueño aún no puede ver al socio (no es de su gimnasio).</summary>
        public string NombreSocio { get; set; } = string.Empty;
        public string DniDeclarado { get; set; } = string.Empty;
        public EstadoSolicitudVinculacion Estado { get; private set; } = EstadoSolicitudVinculacion.Pendiente;
        public Guid? ResueltaPorUsuarioId { get; private set; }
        public DateTime? FechaResolucion { get; private set; }
        public string? Motivo { get; private set; }

        public void Resolver(bool aprobar, Guid usuarioId, string? motivo)
        {
            if (Estado != EstadoSolicitudVinculacion.Pendiente)
                throw new ReglaNegocioException("La solicitud ya fue resuelta.");

            Estado = aprobar ? EstadoSolicitudVinculacion.Aprobada : EstadoSolicitudVinculacion.Rechazada;
            ResueltaPorUsuarioId = usuarioId;
            FechaResolucion = DateTime.UtcNow;
            Motivo = motivo;
        }
    }
}