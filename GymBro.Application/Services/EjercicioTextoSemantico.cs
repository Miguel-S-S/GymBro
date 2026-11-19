namespace GymBro.Application.Services
{
    /// <summary>
    /// Texto que se vectoriza para un ejercicio (Etapa 9a-2). Es el ÚNICO lugar de C# que lo compone.
    /// Debe coincidir con el de GymBro.IA/generador_embeddings.py, que tiene su propia copia:
    ///   f"Ejercicio: {nombre}. Zona objetivo: {zona}. Músculo principal: {musculo}. Técnica y ejecución: {ejecucion}"
    /// Si se cambia uno, hay que cambiar el otro (lo fija la prueba EjercicioTextoSemanticoTests).
    /// </summary>
    public static class EjercicioTextoSemantico
    {
        public static string Componer(string nombre, string zona, string musculo, string ejecucion) =>
            $"Ejercicio: {nombre}. Zona objetivo: {zona}. Músculo principal: {musculo}. Técnica y ejecución: {ejecucion}";
    }

    /// <summary>Configuración de la generación del embedding al guardar (sección "Embeddings" de appsettings).</summary>
    public class EmbeddingConfig
    {
        /// <summary>Tiempo máximo de espera al microservicio de IA. Decisión de diseño: 5 s.</summary>
        public int TiempoMaximoSegundos { get; set; } = 5;

        /// <summary>Dimensión de la columna ejercicios.embedding (vector(768)).</summary>
        public const int Dimension = 768;
    }
}
