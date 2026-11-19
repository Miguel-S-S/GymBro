namespace GymBro.Domain.Enums
{
    // Etapa 10a: enums de las alertas de seguridad. Se guardan como texto, con CHECK en la base (GuardaEnums).

    public enum TipoAlerta
    {
        Alerta = 1,
        Sugerencia = 2
    }

    public enum SeveridadAlerta
    {
        Informativa = 1,
        Advertencia = 2
    }

    public enum EstadoAlerta
    {
        Pendiente = 1,
        Vista = 2
    }

    /// <summary>Códigos de las reglas que generan alertas (no son nombres de enum porque llevan guiones).</summary>
    public static class CodigosRegla
    {
        public const string RirMayorAlPlanificado = "R-RIR-1";
        public const string Progresion = "R-PROG-1";
        public const string FcReposo = "R-FC-1";

        public static readonly string[] Todos = { RirMayorAlPlanificado, Progresion, FcReposo };
    }
}
