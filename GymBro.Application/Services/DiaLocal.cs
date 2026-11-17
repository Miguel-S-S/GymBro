namespace GymBro.Application.Services
{
    /// <summary>
    /// Día local de los socios (Etapa 8b). El servidor y la base trabajan en UTC (SesionEntrenamiento.Inicio es
    /// timestamptz y los clientes lo envían con toISOString()); el día del registro se calcula en esta zona.
    /// </summary>
    public static class DiaLocal
    {
        public const string ZonaHoraria = "America/Argentina/Buenos_Aires";

        private static readonly TimeZoneInfo Zona = TimeZoneInfo.FindSystemTimeZoneById(ZonaHoraria);

        /// <summary>Hoy, en el día local, según el reloj inyectado.</summary>
        public static DateOnly Hoy(TimeProvider reloj) => DeInstante(reloj.GetUtcNow());

        /// <summary>Día local de un instante.</summary>
        public static DateOnly DeInstante(DateTimeOffset instante) =>
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instante, Zona).DateTime);

        /// <summary>
        /// Día local de una fecha UTC guardada en la base (por ejemplo SesionEntrenamiento.Inicio):
        /// es la clave para asociar una sesión con el registro de bienestar de ese día.
        /// </summary>
        public static DateOnly DeUtc(DateTime utc) =>
            DeInstante(new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)));
    }
}
