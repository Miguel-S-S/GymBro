namespace GymBro.Domain.Enums
{
    public enum RolUsuario
    {
        SuperAdministrador = 1,
        AdministradorGimnasio = 2,
        Entrenador = 3,
        Socio = 4
    }

    public static class Roles
    {
        public const string SuperAdministrador = nameof(RolUsuario.SuperAdministrador);
        public const string AdministradorGimnasio = nameof(RolUsuario.AdministradorGimnasio);
        public const string Entrenador = nameof(RolUsuario.Entrenador);
        public const string Socio = nameof(RolUsuario.Socio);

        /// <summary>Personal del gimnasio que opera sobre los socios.</summary>
        public const string PersonalGimnasio = AdministradorGimnasio + "," + Entrenador;
    }
}