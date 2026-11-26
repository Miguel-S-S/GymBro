using GymBro.Domain.Enums;

namespace GymBro.Domain.Entities
{
    /// <summary>
    /// Etapa 10b-1: puntaje de riesgo de abandono de un socio en una fecha, calculado por lotes con un modelo de
    /// GymBro.Analytics (script puntuar_abandono.py, rol de base de datos propio con permiso de insercion). Es un puntaje
    /// descriptivo de un modelo entrenado con datos sinteticos: no se muestra al Socio y no hay endpoint que lo exponga.
    /// </summary>
    public class PrediccionAbandono : EntidadAuditable
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid SocioId { get; set; }
        public Socio Socio { get; set; } = null!;

        /// <summary>Fecha de corte de las variables (dia local).</summary>
        public DateOnly Fecha { get; set; }

        /// <summary>Puntaje entre 0 y 1 (probabilidad estimada por el modelo en sus datos de entrenamiento).</summary>
        public decimal Puntaje { get; set; }

        /// <summary>Version del modelo que produjo el puntaje (por ejemplo, abandono-bosque-v1-20261125-...).</summary>
        public string VersionModelo { get; set; } = string.Empty;

        /// <summary>Origen de los datos del socio puntuado (hoy, solo Sintetico).</summary>
        public OrigenDatos OrigenDatos { get; set; } = OrigenDatos.Sintetico;
    }
}
