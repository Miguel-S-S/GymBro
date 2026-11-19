namespace GymBro.Application.Abstractions
{
    /// <summary>
    /// Registro de eventos técnicos (Etapa 10a), para que la capa de aplicación no dependa de un proveedor de logging.
    /// La implementación (GymBro.Api) usa ILogger. Nunca se deben pasar datos de salud en los mensajes.
    /// </summary>
    public interface IRegistroEventos
    {
        void Advertencia(string mensaje);
        void Error(string mensaje, Exception excepcion);
    }
}
