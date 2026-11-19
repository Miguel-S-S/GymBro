namespace GymBro.Domain.Entities
{
    /// <summary>
    /// Tipo de equipamiento que un gimnasio declara tener (Etapa 9a-2). Quitarlo es una baja lógica; volver a marcarlo
    /// inserta una fila nueva (la anterior queda como historial). Un solo registro vigente por gimnasio y tipo.
    /// </summary>
    public class EquipamientoGimnasio : EntidadAuditable
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid GimnasioId { get; set; }
        public Guid TipoEquipamientoId { get; set; }
        public TipoEquipamiento TipoEquipamiento { get; set; } = null!;

        /// <summary>Cantidad opcional (1 o más si se informa).</summary>
        public int? Cantidad { get; set; }
    }
}
