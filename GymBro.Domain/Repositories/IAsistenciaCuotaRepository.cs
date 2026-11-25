using GymBro.Domain.Entities;

namespace GymBro.Domain.Repositories
{
    /// <summary>Fila de listado con el nombre del socio (sin datos de salud).</summary>
    public sealed record FilaAsistencia(Asistencia Asistencia, string SocioNombre, string GimnasioNombre);

    /// <summary>Fila de listado de cuotas con el nombre del socio (sin datos de salud).</summary>
    public sealed record FilaCuota(CuotaSocio Cuota, string SocioNombre, string GimnasioNombre);

    /// <summary>Etapa 11: asistencias. Las consultas pasan por el filtro entre gimnasios del DbContext.</summary>
    public interface IAsistenciaRepository
    {
        Task<Asistencia?> GetDelDiaAsync(Guid socioId, DateOnly fecha);

        /// <summary>Asistencias de un dia; gimnasioId null = todos los visibles (solo el Administrador de la Plataforma).</summary>
        Task<IReadOnlyList<FilaAsistencia>> ListarAsync(DateOnly fecha, Guid? gimnasioId);

        void Agregar(Asistencia asistencia);

        /// <summary>Persiste. Devuelve false si el indice unico (socio, dia) rechazo un duplicado concurrente.</summary>
        Task<bool> GuardarCambiosAsync();

        /// <summary>Descarta del contexto una asistencia agregada que no se pudo guardar.</summary>
        void Descartar(Asistencia asistencia);
    }

    /// <summary>Etapa 11: cuotas. Las consultas pasan por el filtro entre gimnasios del DbContext.</summary>
    public interface ICuotaRepository
    {
        Task<CuotaSocio?> GetForUpdateAsync(Guid id);

        /// <summary>Socios con cuota ya generada en el periodo, dentro del gimnasio.</summary>
        Task<HashSet<Guid>> SociosConCuotaAsync(Guid gimnasioId, int anio, int mes);

        Task<IReadOnlyList<FilaCuota>> ListarPeriodoAsync(int anio, int mes, Guid? gimnasioId);

        /// <summary>Pendientes con vencimiento anterior a hoy (morosidad derivada).</summary>
        Task<IReadOnlyList<FilaCuota>> ListarVencidasAsync(DateOnly hoy, Guid? gimnasioId);

        Task<IReadOnlyList<CuotaSocio>> ListarDelSocioAsync(Guid socioId);

        void AgregarRango(IEnumerable<CuotaSocio> cuotas);

        /// <summary>Persiste. Devuelve false si el indice unico (socio, periodo) rechazo un duplicado concurrente.</summary>
        Task<bool> GuardarCambiosAsync();
    }
}
