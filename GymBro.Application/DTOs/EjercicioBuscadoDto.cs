namespace GymBro.Application.DTOs
{
    public class EjercicioBuscadoDto
    {
        public Guid Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string ZonaObjetivo { get; set; } = string.Empty;
        public string MusculoPrincipal { get; set; } = string.Empty;
        public string Equipamiento { get; set; } = string.Empty;
    }

    /// <summary>
    /// Etapa 9b-1: cuerpo de POST /api/Ejercicios/busqueda-inteligente. La consulta viaja en el cuerpo (no en la URL, que
    /// queda en registros de servidores y proxies). Los filtros son duros: nunca se devuelve un ejercicio inactivo, de otro
    /// gimnasio o sin equipamiento disponible en el gimnasio.
    /// </summary>
    public class BusquedaSemanticaRequest
    {
        public string? Consulta { get; set; }

        /// <summary>Cantidad de resultados (1 a 50; por defecto 5).</summary>
        public int Cantidad { get; set; } = 5;

        /// <summary>Zona objetivo opcional (por ejemplo "Legs"), comparada sin distinguir mayusculas.</summary>
        public string? Zona { get; set; }

        /// <summary>
        /// Gimnasio de referencia. Solo lo indica el Administrador de la Plataforma (sin el, solo ejercicios globales y sin
        /// filtro de equipamiento); el personal usa el de su sesion y el Socio el de su ficha.
        /// </summary>
        public Guid? GimnasioId { get; set; }

        /// <summary>
        /// Claves de tipo de equipamiento opcionales (por ejemplo "MANCUERNA"). Restringen aun mas el resultado; nunca amplian
        /// lo disponible en el gimnasio.
        /// </summary>
        public List<string>? Equipamiento { get; set; }
    }
}
