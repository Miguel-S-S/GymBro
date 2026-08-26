using GymBro.Domain.Enums;

namespace GymBro.Domain.Entities
{
    public class Socio : EntidadAuditable
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Nombre { get; set; } = string.Empty;
        public string Apellido { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Genero { get; set; } = string.Empty;
        public DateTime FechaNacimiento { get; set; }

        public ObjetivoEntrenamiento Objetivo { get; set; }
        public NivelExperiencia Nivel { get; set; }

        public ICollection<MedicionFisica> HistorialFisico { get; set; } = new List<MedicionFisica>();
        public ICollection<SesionEntrenamiento> Sesiones { get; set; }
          = new List<SesionEntrenamiento>();

        public string NombreCompleto =>
           $"{Nombre} {Apellido}";

        public MedicionFisica? UltimaMedicion =>
            HistorialFisico
                .OrderByDescending(m => m.FechaAlta)
                .FirstOrDefault();
    }
}