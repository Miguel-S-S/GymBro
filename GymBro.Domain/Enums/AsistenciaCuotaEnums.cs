namespace GymBro.Domain.Enums
{
    /// <summary>Etapa 11: como se registro la asistencia. Se guarda como texto.</summary>
    public enum MetodoAsistencia
    {
        /// <summary>El personal valido el codigo QR firmado que muestra el Socio en su PWA.</summary>
        Qr = 1,
        /// <summary>El personal eligio al socio en la lista (sin QR).</summary>
        Manual = 2
    }

    /// <summary>
    /// Etapa 11: estado guardado de una cuota. "Vencida" o "morosa" no es un estado: se deriva de una cuota Pendiente con
    /// vencimiento anterior al dia local de hoy (CuotaSocio.EstaVencida). Se guarda como texto.
    /// </summary>
    public enum EstadoCuota
    {
        Pendiente = 1,
        Pagada = 2
    }
}
