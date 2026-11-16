namespace GymBro.Domain.Entities
{
    /// <summary>
    /// Invitación de un solo uso que el Dueño entrega al socio dado de alta de forma presencial.
    /// Con ella el socio crea su contraseña, completa sus datos y acepta el tratamiento de datos de salud.
    /// Solo se guarda el hash del código: el valor en claro se muestra una única vez al emitirla.
    /// </summary>
    public class InvitacionSocio : EntidadAuditable
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid GimnasioId { get; set; }
        public Gimnasio Gimnasio { get; set; } = null!;
        public Guid SocioId { get; set; }
        public Socio Socio { get; set; } = null!;

        /// <summary>SHA-256 (hex) del código entregado al socio.</summary>
        public string TokenHash { get; set; } = string.Empty;
        public DateTime ExpiraUtc { get; set; }
        public DateTime? UsadaUtc { get; private set; }
        public DateTime? RevocadaUtc { get; private set; }

        public bool EstaVigente(DateTime ahoraUtc) => UsadaUtc is null && RevocadaUtc is null && ExpiraUtc > ahoraUtc;

        public void MarcarUsada() => UsadaUtc = DateTime.UtcNow;
        public void Revocar() => RevocadaUtc ??= DateTime.UtcNow;
    }
}
