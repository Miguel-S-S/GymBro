using GymBro.Domain.Enums;
using System;
using System.Collections.Generic;
using GymBro.Domain.Exceptions;

namespace GymBro.Domain.Entities
{
    public class PrescripcionEntrenamiento : EntidadAuditable
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid SocioId { get; set; }
        public Socio Socio { get; set; } = null!;

        public EstadoPrescripcion Estado { get; set; } = EstadoPrescripcion.Creada;

        public string OrigenGeneracion { get; set; } = string.Empty;

        public string? Observaciones { get; set; }

        public ICollection<PrescripcionEjercicio> EjerciciosPrescritos { get; set; } = new List<PrescripcionEjercicio>();
        public ICollection<SesionEntrenamiento> Sesiones { get; set; } = new List<SesionEntrenamiento>();
        public ICollection<RevisionPrescripcion> Revisiones { get; set; } = new List<RevisionPrescripcion>();

        /// <summary>Se invoca al generar la rutina: define si pasa a revisión humana o queda aprobada.</summary>
        public void DefinirEstadoInicial(bool requiereRevisionHumana) =>
            Estado = requiereRevisionHumana ? EstadoPrescripcion.PendienteRevision : EstadoPrescripcion.Aprobada;

        /// <summary>Cambia el estado y devuelve la traza; quien llama debe persistirla (repositorio).</summary>
        public RevisionPrescripcion Revisar(EstadoPrescripcion decision, Guid revisorUsuarioId, string? comentario)
        {
            if (Estado != EstadoPrescripcion.PendienteRevision)
                throw new ReglaNegocioException("La rutina no está pendiente de revisión.");
            if (decision is not (EstadoPrescripcion.Aprobada or EstadoPrescripcion.Editada or EstadoPrescripcion.Rechazada))
                throw new ReglaNegocioException("Decisión de revisión inválida.");
            if (decision == EstadoPrescripcion.Rechazada && string.IsNullOrWhiteSpace(comentario))
                throw new ReglaNegocioException("Al rechazar una rutina debe indicar el motivo.");

            Estado = decision;
            var revision = new RevisionPrescripcion
            {
                PrescripcionEntrenamientoId = Id,
                RevisorUsuarioId = revisorUsuarioId,
                Decision = decision,
                Comentario = comentario
            };
            return revision;
        }
    }
}