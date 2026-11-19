using System;

namespace GymBro.Application.DTOs
{
    public class EjercicioResumenDto
    {
        public Guid Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string ZonaObjetivo { get; set; } = string.Empty;
        public string MusculoPrincipal { get; set; } = string.Empty;
        public string Equipamiento { get; set; } = string.Empty;
        public int Dificultad { get; set; }
        /// <summary>"Repeticiones" o "Segundos" (Etapa 9a-1).</summary>
        public string TipoMedida { get; set; } = "Repeticiones";
        /// <summary>"Dataset", "Proyecto" o "Gimnasio" (Etapa 9a-1).</summary>
        public string Origen { get; set; } = "Dataset";
        /// <summary>Gimnasio dueño del ejercicio; null para los globales (Etapa 9a-2).</summary>
        public Guid? GimnasioId { get; set; }
        public Guid? TipoEquipamientoId { get; set; }
        /// <summary>Nombre del tipo de equipamiento normalizado (Etapa 9a-2).</summary>
        public string? TipoEquipamiento { get; set; }
    }

    public class EjercicioDetalleDto
    {
        public Guid Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Equipamiento { get; set; } = string.Empty;
        public string ZonaObjetivo { get; set; } = string.Empty;
        public string MusculoPrincipal { get; set; } = string.Empty;
        public string Sinergistas { get; set; } = string.Empty;
        public string Estabilizadores { get; set; } = string.Empty;
        public string Preparacion { get; set; } = string.Empty;
        public string Ejecucion { get; set; } = string.Empty;
        public int Dificultad { get; set; }
        /// <summary>"Repeticiones" o "Segundos" (Etapa 9a-1).</summary>
        public string TipoMedida { get; set; } = "Repeticiones";
        /// <summary>"Dataset", "Proyecto" o "Gimnasio" (Etapa 9a-1).</summary>
        public string Origen { get; set; } = "Dataset";
        /// <summary>Gimnasio dueño del ejercicio; null para los globales (Etapa 9a-2).</summary>
        public Guid? GimnasioId { get; set; }
        public Guid? TipoEquipamientoId { get; set; }
        /// <summary>Nombre del tipo de equipamiento normalizado (Etapa 9a-2).</summary>
        public string? TipoEquipamiento { get; set; }
    }
}
