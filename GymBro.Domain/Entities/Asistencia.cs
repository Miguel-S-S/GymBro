using GymBro.Domain.Enums;

namespace GymBro.Domain.Entities
{
    /// <summary>
    /// Etapa 11: ingreso de un socio al gimnasio. Una asistencia por socio y dia local (America/Argentina/Buenos_Aires):
    /// el indice unico (socio_id, fecha) entre las filas vigentes hace idempotente el registro. No es un dato de salud.
    /// </summary>
    public class Asistencia : EntidadAuditable
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid SocioId { get; set; }
        public Socio Socio { get; set; } = null!;

        /// <summary>Gimnasio donde se registro (el del socio en ese momento). Es la clave del filtro entre gimnasios.</summary>
        public Guid GimnasioId { get; set; }

        /// <summary>Instante del registro, en UTC.</summary>
        public DateTime Instante { get; set; }

        /// <summary>Dia local del instante, calculado en el servidor. Clave de la unicidad diaria.</summary>
        public DateOnly Fecha { get; set; }

        public MetodoAsistencia Metodo { get; set; }

        /// <summary>Cuenta del personal que registro la asistencia (Dueno o Personal Trainer).</summary>
        public Guid RegistradoPorUsuarioId { get; set; }

        /// <summary>Procedencia del dato (Etapa 7e: el generador sintetico usa Sintetico).</summary>
        public OrigenDatos OrigenDatos { get; set; } = OrigenDatos.Real;
    }
}
