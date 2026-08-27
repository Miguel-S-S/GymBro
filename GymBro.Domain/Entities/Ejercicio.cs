using Pgvector;

namespace GymBro.Domain.Entities
{
    public class Ejercicio : EntidadAuditable
    {
        public Guid Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Equipamiento { get; set; } = string.Empty;

        // Anatomía y Biomecánica
        public string ZonaObjetivo { get; set; } = string.Empty; 
        public string MusculoPrincipal { get; set; } = string.Empty;
        public string Sinergistas { get; set; } = string.Empty;
        public string Estabilizadores { get; set; } = string.Empty;

        // Ejecución
        public string Preparacion { get; set; } = string.Empty;
        public string Ejecucion { get; set; } = string.Empty;
        public int Dificultad { get; set; }

        // Motor IA
        public Vector? Embedding { get; set; }
    }
}