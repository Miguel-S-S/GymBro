namespace GymBro.Domain.Enums
{
    /// <summary>
    /// Estado administrativo de la ficha del socio (Etapa 9a-2b). Es informativo: no bloquea el inicio de sesión,
    /// las sesiones ni las rutinas. Lo cambia el Dueño del gimnasio; el Administrador de la Plataforma, por script.
    /// Se guarda como texto.
    /// </summary>
    public enum EstadoSocio
    {
        Activo = 1,
        Inactivo = 2
    }
}
