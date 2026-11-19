namespace GymBro.Domain.Entities
{
    /// <summary>
    /// Tipo de equipamiento (Etapa 9a-2). GimnasioId nulo: tipo global del catálogo (Id fijo, ver CatalogoEquipamiento).
    /// GimnasioId con valor: tipo propio creado por el Dueño de ese gimnasio, visible solo en él.
    /// </summary>
    public class TipoEquipamiento : EntidadAuditable
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>Texto estable (por ejemplo, MANCUERNA). Único entre los globales y, para los propios, dentro del gimnasio.</summary>
        public string Clave { get; set; } = string.Empty;

        /// <summary>Nombre en español: es lo que ve el usuario.</summary>
        public string Nombre { get; set; } = string.Empty;

        /// <summary>Disponible en cualquier gimnasio aunque no se marque (peso corporal, estiramiento).</summary>
        public bool SiempreDisponible { get; set; }

        public Guid? GimnasioId { get; set; }
    }
}
