namespace GymBro.Application.DTOs
{
    /// <summary>
    /// Alta o edición de un ejercicio propio del gimnasio (Etapa 9a-2). El gimnasio sale del JWT y el origen lo fija el
    /// servidor: el cuerpo no tiene esos campos (si se envían, se ignoran).
    /// </summary>
    public class EjercicioGimnasioRequest
    {
        public string Nombre { get; set; } = string.Empty;
        public string ZonaObjetivo { get; set; } = string.Empty;
        public Guid TipoEquipamientoId { get; set; }
        public string MusculoPrincipal { get; set; } = string.Empty;
        public string? Sinergistas { get; set; }
        public string? Estabilizadores { get; set; }
        public string? Preparacion { get; set; }
        public string Ejecucion { get; set; } = string.Empty;
        /// <summary>"Repeticiones" o "Segundos".</summary>
        public string TipoMedida { get; set; } = "Repeticiones";
        public int Dificultad { get; set; }
    }

    /// <summary>Tipo de equipamiento visto desde un gimnasio, con su estado de marcado (Etapa 9a-2).</summary>
    public class EquipamientoItemDto
    {
        public Guid TipoEquipamientoId { get; set; }
        public string Clave { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        /// <summary>Incluido siempre (peso corporal, estiramiento): no se marca ni se quita.</summary>
        public bool SiempreDisponible { get; set; }
        /// <summary>Tipo creado por el gimnasio (no es del catálogo global).</summary>
        public bool Propio { get; set; }
        public bool Marcado { get; set; }
        public int? Cantidad { get; set; }
    }

    public class EquipamientoGimnasioDto
    {
        public Guid GimnasioId { get; set; }
        public string NombreGimnasio { get; set; } = string.Empty;
        /// <summary>true si quien consulta puede modificarlo (solo el Dueño del gimnasio).</summary>
        public bool Editable { get; set; }
        public List<EquipamientoItemDto> Items { get; set; } = new();
    }

    public class EquipamientoMarcadoRequest
    {
        public Guid TipoEquipamientoId { get; set; }
        public int? Cantidad { get; set; }
    }

    /// <summary>Reemplaza el conjunto de tipos marcados del gimnasio.</summary>
    public class EquipamientoReemplazoRequest
    {
        public List<EquipamientoMarcadoRequest> Items { get; set; } = new();
    }

    public class TipoEquipamientoCreateRequest
    {
        public string Nombre { get; set; } = string.Empty;
    }
}
