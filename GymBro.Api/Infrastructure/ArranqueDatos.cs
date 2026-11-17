namespace GymBro.Api.Infrastructure
{
    /// <summary>
    /// Decisiones de arranque sobre la carga de datos (Etapa 8c, T3).
    /// Las migraciones se aplican siempre; los datasets CSV solo se siembran si Datasets:SembrarAlIniciar es verdadero.
    /// </summary>
    public static class ArranqueDatos
    {
        public const string ClaveSembrarAlIniciar = "Datasets:SembrarAlIniciar";

        /// <summary>
        /// Valor de Datasets:SembrarAlIniciar si está configurado; si no, verdadero solo en Development.
        /// </summary>
        public static bool DebeSembrarDatasets(IConfiguration configuracion, IHostEnvironment entorno) =>
            configuracion.GetValue<bool?>(ClaveSembrarAlIniciar) ?? entorno.IsDevelopment();
    }
}
