namespace GymBro.Domain.Entities
{
    /// <summary>Tenant del sistema. Cada gimnasio ve únicamente sus propios socios y personal.</summary>
    public class Gimnasio : EntidadAuditable
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Nombre { get; set; } = string.Empty;

        /// <summary>Código corto que el socio ingresa en la PWA para pedir vincularse.</summary>
        public string Codigo { get; set; } = string.Empty;

        /// <summary>Si es true, las rutinas generadas por la IA para socios del gimnasio requieren revisión humana.</summary>
        public bool RequiereAprobacionRutinas { get; set; } = true;

        public bool Activo { get; set; } = true;

        public ICollection<Socio> Socios { get; set; } = new List<Socio>();
    }
}