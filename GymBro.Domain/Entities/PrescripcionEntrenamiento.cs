using GymBro.Domain.Enums;
using System;
using System.Collections.Generic;

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
    }
}
