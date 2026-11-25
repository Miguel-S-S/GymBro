using GymBro.Domain.Entities;

namespace GymBro.Application.Sintetico
{
    /// <summary>
    /// Etapa 7e: operaciones directas sobre la base que el generador no puede hacer con los servicios de negocio: el alta del
    /// gimnasio, de las cuentas y de las fichas con identificadores derivados de la semilla, el resumen (hash) del conjunto y
    /// la eliminacion de los datos sinteticos. Todo lo demas pasa por los servicios reales.
    /// </summary>
    public interface IAlmacenSintetico
    {
        /// <summary>Prefijo del codigo de los gimnasios sinteticos ("SINT" + 8 hexadecimales de la semilla).</summary>
        public const string PrefijoCodigo = "SINT";

        Task<bool> ExisteGimnasioAsync(Guid gimnasioId);

        /// <summary>Alta del gimnasio (sin revision humana de rutinas, para que la rutina generada quede vigente) y del Dueno.</summary>
        Task CrearGimnasioAsync(Gimnasio gimnasio, Usuario dueno);

        /// <summary>Alta de la ficha (Sintetico, con consentimiento) y de su cuenta de Socio (sin contrasena utilizable).</summary>
        Task CrearSocioAsync(Socio socio, Usuario cuenta);

        /// <summary>Resumen SHA-256 del contenido generado para el gimnasio (sin los ids internos de los servicios).</summary>
        Task<ResumenSintetico> ResumirAsync(Guid gimnasioId);

        /// <summary>
        /// Elimina los gimnasios sinteticos (codigo con el prefijo) y, de ellos, solo los socios con origen Sintetico y todos
        /// sus datos; si queda algun socio de otro origen en el gimnasio, el gimnasio no se elimina. La auditoria no se borra.
        /// </summary>
        Task<EliminacionSintetica> EliminarAsync();
    }

    public sealed record ResumenSintetico(string Sha256, int Socios, int Sesiones, int Series, int Prescripciones, int Bienestar,
        int Asistencias, int Cuotas, int Alertas);

    public sealed record EliminacionSintetica(int Gimnasios, int Socios, int Sesiones, int Asistencias, int Cuotas, int Usuarios);
}
