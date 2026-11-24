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

        // ---- Etapa 7d: trazabilidad del protocolo y del estudio piloto ----

        /// <summary>Código del protocolo que generó la rutina (PIERNAS). Null en las rutinas que no salen de un protocolo.</summary>
        public string? ProtocoloCodigo { get; set; }

        /// <summary>Versión del protocolo (por ejemplo, piernas-1.0).</summary>
        public string? ProtocoloVersion { get; set; }

        /// <summary>Número de generación del socio en el protocolo (1, 2, ...). No se reutiliza aunque la rutina se rechace.</summary>
        public int? NumeroGeneracion { get; set; }

        /// <summary>
        /// Procedencia de los datos, copiada del socio al generar el protocolo. Las rutinas de otros generadores quedan con
        /// el valor inicial (Real), que es conservador: el estudio filtra además por el origen del socio.
        /// </summary>
        public OrigenDatos OrigenDatos { get; set; } = OrigenDatos.Real;

        /// <summary>Marca manual para dejar la rutina fuera de los indicadores del estudio.</summary>
        public bool ExcluidaEstudio { get; set; }

        /// <summary>True si una revisión con ajustes cambió algún valor de la dosis (Etapa 7d, 6.4).</summary>
        public bool EsModificada { get; set; }

        /// <summary>JSON (jsonb) con las entradas usadas en la generación, sin datos personales identificatorios.</summary>
        public string? EntradaSnapshot { get; set; }

        public ICollection<PrescripcionEjercicio> EjerciciosPrescritos { get; set; } = new List<PrescripcionEjercicio>();
        public ICollection<SesionEntrenamiento> Sesiones { get; set; } = new List<SesionEntrenamiento>();
        public ICollection<RevisionPrescripcion> Revisiones { get; set; } = new List<RevisionPrescripcion>();

        /// <summary>
        /// Rutina vigente: la que el socio puede ejecutar. Decisión del 16/11/2026: Aprobada o Editada
        /// (Editada = aprobada con ajustes del entrenador).
        /// </summary>
        public bool EsVigente() => Estado is EstadoPrescripcion.Aprobada or EstadoPrescripcion.Editada;

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