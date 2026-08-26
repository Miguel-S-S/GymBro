using Pgvector;

namespace GymBro.Domain.Entities
{
    public class Ejercicio : EntidadAuditable
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Nombre { get; set; } = string.Empty;
        public string GrupoMuscular { get; set; } = string.Empty;
        public string Equipamiento { get; set; } = string.Empty;

        public Vector? Embedding { get; set; }
    }
}