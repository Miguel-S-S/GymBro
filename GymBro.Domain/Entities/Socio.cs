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

        /// <summary>Estado administrativo, informativo (Etapa 9a-2b). Por defecto Activo.</summary>
        public EstadoSocio Estado { get; set; } = EstadoSocio.Activo;

        /// <summary>Consentimiento expreso para tratar datos de salud (Ley 25.326, art. 5 y 7).</summary>
        public DateTime? ConsentimientoDatosSaludFecha { get; set; }

        public string Genero { get; set; } = string.Empty;
        public DateTime FechaNacimiento { get; set; }

        public ObjetivoEntrenamiento Objetivo { get; set; }
        public NivelExperiencia Nivel { get; set; }

        /// <summary>
        /// Etapa 7d (D2): limitación articular de rodilla. Dato de salud: mismas reglas de acceso y consentimiento que las
        /// mediciones (el Administrador de la Plataforma no accede).
        /// </summary>
        public NivelLimitacion LimitacionRodilla { get; set; } = NivelLimitacion.SinLimitacion;

        /// <summary>Etapa 7d (D2): limitación articular de la zona lumbar. Dato de salud (ver <see cref="LimitacionRodilla"/>).</summary>
        public NivelLimitacion LimitacionLumbar { get; set; } = NivelLimitacion.SinLimitacion;

        /// <summary>
        /// Etapa 7d (P1, P7): procedencia de los datos. Quien crea el socio asigna el valor explícito (Real en el flujo
        /// normal, DatasetExterno en DataSeeder, Sintetico en el generador de la 7e). El valor inicial Real es conservador:
        /// los KPIs del estudio incluyen solo Sintetico. Las filas anteriores a la migración quedan en DatasetExterno.
        /// </summary>
        public OrigenDatos OrigenDatos { get; set; } = OrigenDatos.Real;

        public ICollection<MedicionFisica> HistorialFisico { get; set; } = new List<MedicionFisica>();
        public ICollection<PrescripcionEntrenamiento> Prescripciones { get; set; } = new List<PrescripcionEntrenamiento>();
        public ICollection<SesionEntrenamiento> Sesiones { get; set; } = new List<SesionEntrenamiento>();
        public string NombreCompleto =>
            $"{Nombre} {Apellido}";
        public MedicionFisica? UltimaMedicion => HistorialFisico.OrderByDescending(m => m.FechaAlta).FirstOrDefault();
    }
}