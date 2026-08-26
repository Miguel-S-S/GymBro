namespace GymBro.Domain.Entities
{
    public abstract class EntidadAuditable
    {
        public DateTime FechaAlta { get; set; } = DateTime.UtcNow;
        public int UsuarioAlta { get; set; } = 1; 

        public DateTime? FechaModificacion { get; set; }
        public int? UsuarioModificacion { get; set; }

        public DateTime? FechaBaja { get; set; }
        public int? UsuarioBaja { get; set; }
    }
}