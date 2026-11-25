namespace GymBro.Application.DTOs
{
    /// <summary>Etapa 9b-2: cuerpo de POST /api/revisiones/{id}/lineas/{lineaId}/sustitutos.</summary>
    public class SugerirSustitutosRequest
    {
        /// <summary>Motivo de la sustitución (por ejemplo, "molestia lumbar" o "la máquina está en reparación").</summary>
        public string? Motivo { get; set; }
    }

    public class SustitutoSugeridoDto
    {
        public Guid EjercicioId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string ZonaObjetivo { get; set; } = string.Empty;
        public string Equipamiento { get; set; } = string.Empty;
        /// <summary>Justificación del modelo (texto generado; revisar antes de usar).</summary>
        public string Justificacion { get; set; } = string.Empty;
    }

    public class SustitutoRechazadoDto
    {
        public Guid EjercicioId { get; set; }
        /// <summary>Códigos de R-LIM que violaría la rutina con este sustituto.</summary>
        public List<string> Codigos { get; set; } = new();
    }

    /// <summary>
    /// Etapa 9b-2: sugerencia (no aplicada) de sustitutos de una línea. Si la salida del modelo no es JSON con la forma
    /// pedida o elige un id fuera de los candidatos recuperados, se descarta completa (Descartada = true).
    /// </summary>
    public class SugerenciaSustitutosDto
    {
        public Guid PrescripcionId { get; set; }
        public Guid LineaId { get; set; }
        public Guid EjercicioActualId { get; set; }
        public string EjercicioActual { get; set; } = string.Empty;
        /// <summary>Candidatos recuperados por la búsqueda semántica con filtros duros (el modelo solo elige entre estos).</summary>
        public List<EjercicioBuscadoDto> Candidatos { get; set; } = new();
        public List<SustitutoSugeridoDto> Sugerencias { get; set; } = new();
        public List<SustitutoRechazadoDto> RechazadasPorLimites { get; set; } = new();
        public bool RespetaConjunto { get; set; }
        public bool Descartada { get; set; }
        /// <summary>"sin_candidatos", "salida_mal_formada" o "candidato_fuera_del_conjunto".</summary>
        public string? MotivoDescarte { get; set; }
        public string? Modelo { get; set; }
        public double? LatenciaMs { get; set; }
    }
}
