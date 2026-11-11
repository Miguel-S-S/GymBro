using GymBro.Domain.Enums;

namespace GymBro.Domain.Entities
{
    public class Socio : EntidadAuditable
    {
        public Guid Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Apellido { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        /// <summary>DNI: clave de la validación cruzada de vinculación a un gimnasio.</summary>
        public string? Dni { get; set; }

        /// <summary>null = socio independiente (sin gimnasio ni revisión humana).</summary>
        public Guid? GimnasioId { get; set; }
        public Gimnasio? Gimnasio { get; set; }

        /// <summary>Consentimiento expreso para tratar datos de salud (Ley 25.326, art. 5 y 7).</summary>
        public DateTime? ConsentimientoDatosSaludFecha { get; set; }

        public string Genero { get; set; } = string.Empty;
        public DateTime FechaNacimiento { get; set; }

        public ObjetivoEntrenamiento Objetivo { get; set; }
        public NivelExperiencia Nivel { get; set; }

        public ICollection<MedicionFisica> HistorialFisico { get; set; } = new List<MedicionFisica>();
        public ICollection<PrescripcionEntrenamiento> Prescripciones { get; set; } = new List<PrescripcionEntrenamiento>();
        public ICollection<SesionEntrenamiento> Sesiones { get; set; } = new List<SesionEntrenamiento>();
        public string NombreCompleto =>
            $"{Nombre} {Apellido}";
        public MedicionFisica? UltimaMedicion => HistorialFisico.OrderByDescending(m => m.FechaAlta).FirstOrDefault();
    }
}