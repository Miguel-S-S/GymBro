namespace GymBro.Domain.Enums
{
    /// <summary>
    /// Unidad con la que se mide la ejecución de un ejercicio (Etapa 9a-1).
    /// Con Segundos, los campos de "repeticiones" de la prescripción, la dosificación y la serie guardan segundos.
    /// </summary>
    public enum TipoMedida
    {
        Repeticiones = 1,
        Segundos = 2
    }
}
