namespace GymBro.Application.DTOs
{
    /// <summary>
    /// Alta o edición de un ejercicio propio del gimnasio (Etapa 9a-2). El origen y el autor los fija el servidor: el
    /// cuerpo no tiene esos campos (si se envían, se ignoran).
    /// </summary>
    public class EjercicioGimnasioRequest
    {
        /// <summary>
        /// Etapa 9a-2b, solo en el alta. Administrador de la Plataforma: obligatorio (gimnasio destino). Dueño y Personal
        /// Trainer: el gimnasio sale del JWT; si se indica otro, 404. En la edición se ignora (el ejercicio ya tiene gimnasio).
        /// </summary>
        public Guid? GimnasioId { get; set; }

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
        /// <summary>true si quien consulta puede modificarlo (el Dueño del gimnasio o el Administrador de la Plataforma).</summary>
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
        /// <summary>Etapa 9a-2b: obligatorio para el Administrador de la Plataforma; el Dueño usa el de su JWT (otro: 404).</summary>
        public Guid? GimnasioId { get; set; }
        public List<EquipamientoMarcadoRequest> Items { get; set; } = new();
    }

    public class TipoEquipamientoCreateRequest
    {
        /// <summary>Etapa 9a-2b: obligatorio para el Administrador de la Plataforma; el Dueño usa el de su JWT (otro: 404).</summary>
        public Guid? GimnasioId { get; set; }
        public string Nombre { get; set; } = string.Empty;
    }

    /// <summary>Edición del nombre de un tipo de equipamiento (Etapa 9a-2b). La clave no cambia.</summary>
    public class TipoEquipamientoEditRequest
    {
        public string Nombre { get; set; } = string.Empty;
    }

    /// <summary>
    /// Previsualización de un reemplazo de equipamiento (Etapa 9a-2b, O1): ejercicios propios activos del gimnasio que
    /// dejarían de ser factibles para el baseline. Solo lectura.
    /// </summary>
    public class EquipamientoPrevisualizacionDto
    {
        public int Cantidad { get; set; }
        /// <summary>Hasta 10 nombres, ordenados.</summary>
        public List<string> Ejercicios { get; set; } = new();
        /// <summary>Nombres de los tipos que se desmarcarían.</summary>
        public List<string> TiposQuitados { get; set; } = new();
    }

    /// <summary>Desactivar (false) o reactivar (true) un ejercicio (Etapa 9a-2b).</summary>
    public class EstadoEjercicioRequest
    {
        public bool Activo { get; set; }
    }
}
