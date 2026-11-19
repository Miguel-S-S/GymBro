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
        /// <summary>false si está desactivado (Etapa 9a-2b; solo lo ven quienes gestionan el catálogo).</summary>
        public bool Activo { get; set; } = true;
        /// <summary>Autor del ejercicio propio (Etapa 9a-2b); null en los globales y en los anteriores a la etapa.</summary>
        public Guid? CreadoPorUsuarioId { get; set; }
        /// <summary>Nombre del autor, si quien consulta puede verlo ("Propuesto por").</summary>
        public string? CreadoPor { get; set; }
        /// <summary>Etapa 9a-2b: quien consulta puede editarlo (misma regla que EjercicioGimnasioService).</summary>
        public bool PuedeEditar { get; set; }
        /// <summary>Etapa 9a-2b: quien consulta puede desactivarlo o reactivarlo.</summary>
        public bool PuedeCambiarEstado { get; set; }
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
        /// <summary>false si está desactivado (Etapa 9a-2b; solo lo ven quienes gestionan el catálogo).</summary>
        public bool Activo { get; set; } = true;
        /// <summary>Autor del ejercicio propio (Etapa 9a-2b); null en los globales y en los anteriores a la etapa.</summary>
        public Guid? CreadoPorUsuarioId { get; set; }
        /// <summary>Nombre del autor, si quien consulta puede verlo ("Propuesto por").</summary>
        public string? CreadoPor { get; set; }
        /// <summary>Etapa 9a-2b: quien consulta puede editarlo (misma regla que EjercicioGimnasioService).</summary>
        public bool PuedeEditar { get; set; }
        /// <summary>Etapa 9a-2b: quien consulta puede desactivarlo o reactivarlo.</summary>
        public bool PuedeCambiarEstado { get; set; }
    }
}
