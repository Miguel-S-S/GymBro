namespace GymBro.Domain.Enums
{
    // Etapa 7d: enums del protocolo de piernas y de la trazabilidad del estudio. Se guardan como texto, con CHECK en la
    // base (GuardaEnums). Los valores empiezan en 1: el 0 (sin asignar) lo rechaza la guarda de SaveChanges.

    /// <summary>
    /// Limitación articular declarada para la rodilla o la zona lumbar (decisión D2). Es un dato de salud. Los efectos
    /// sobre la selección de ejercicios son decisiones de diseño provisionales, sin validación clínica.
    /// </summary>
    public enum NivelLimitacion
    {
        SinLimitacion = 1,
        Moderada = 2,
        Alta = 3
    }

    /// <summary>
    /// Procedencia de los datos del socio (propuesta P1, decisión P7). Los KPIs del estudio incluyen solo Sintetico;
    /// ValidacionFuncional es el perfil del autor (caso de prueba funcional, fuera de los resultados de la tesis).
    /// </summary>
    public enum OrigenDatos
    {
        Sintetico = 1,
        ValidacionFuncional = 2,
        DatasetExterno = 3,
        Real = 4
    }

    /// <summary>Grupo muscular del protocolo de extremidad inferior.</summary>
    public enum GrupoMuscularPiernas
    {
        Cuadriceps = 1,
        Isquiotibiales = 2,
        Pantorrillas = 3
    }

    /// <summary>Referencia: ejercicio fijo que se repite en cada generación. Variable: rota entre generaciones.</summary>
    public enum RolEjercicioProtocolo
    {
        Referencia = 1,
        Variable = 2
    }

    public enum ClaseEjercicio
    {
        Multiarticular = 1,
        Aislamiento = 2
    }
}
