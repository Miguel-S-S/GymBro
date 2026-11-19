using System.Text.Json;

namespace GymBro.Application.DTOs
{
    /// <summary>Alerta o sugerencia de las reglas de seguridad (Etapa 10a). Dato de salud.</summary>
    public class AlertaSeguridadDto
    {
        public Guid Id { get; set; }
        public Guid SocioId { get; set; }
        public string NombreSocio { get; set; } = string.Empty;
        /// <summary>R-RIR-1, R-PROG-1 o R-FC-1.</summary>
        public string ReglaCodigo { get; set; } = string.Empty;
        /// <summary>Alerta o Sugerencia.</summary>
        public string Tipo { get; set; } = string.Empty;
        /// <summary>Informativa o Advertencia.</summary>
        public string Severidad { get; set; } = string.Empty;
        public string Mensaje { get; set; } = string.Empty;
        /// <summary>Pendiente o Vista.</summary>
        public string Estado { get; set; } = string.Empty;
        public DateTime FechaAlta { get; set; }
        public DateTime? VistaEn { get; set; }
        public Guid? SesionId { get; set; }
        public Guid? SesionEjercicioId { get; set; }
        public Guid? RegistroBienestarId { get; set; }
        /// <summary>Nombre del ejercicio (reglas de sesión).</summary>
        public string? Ejercicio { get; set; }
        /// <summary>Carga sugerida (R-PROG-1).</summary>
        public decimal? CargaSugeridaKg { get; set; }
        /// <summary>Entradas, umbrales aplicados, resultado y versión de las reglas.</summary>
        public JsonElement Detalle { get; set; }
    }

    /// <summary>Violación de un límite duro (R-LIM) de una prescripción.</summary>
    public class ViolacionLimiteDto
    {
        public string Codigo { get; set; } = string.Empty;
        public string Mensaje { get; set; } = string.Empty;
        /// <summary>Orden del ejercicio en la rutina; null si es de la rutina completa.</summary>
        public int? Orden { get; set; }
    }
}
