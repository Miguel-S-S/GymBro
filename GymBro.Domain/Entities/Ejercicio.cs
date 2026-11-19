using GymBro.Domain.Enums;
using Pgvector;

namespace GymBro.Domain.Entities
{
    public class Ejercicio : EntidadAuditable
    {
        public Guid Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Equipamiento { get; set; } = string.Empty;

        // Anatomía y Biomecánica
        public string ZonaObjetivo { get; set; } = string.Empty; 
        public string MusculoPrincipal { get; set; } = string.Empty;
        public string Sinergistas { get; set; } = string.Empty;
        public string Estabilizadores { get; set; } = string.Empty;

        // Ejecución
        public string Preparacion { get; set; } = string.Empty;
        public string Ejecucion { get; set; } = string.Empty;
        public int Dificultad { get; set; }

        /// <summary>Repeticiones o Segundos (Etapa 9a-1). Las filas del dataset quedan en Repeticiones.</summary>
        public TipoMedida TipoMedida { get; set; } = TipoMedida.Repeticiones;

        /// <summary>Dataset (Kaggle), Proyecto (CatalogoProyectoSeeder) o Gimnasio (ejercicio propio, Etapa 9a-2).</summary>
        public OrigenEjercicio Origen { get; set; } = OrigenEjercicio.Dataset;

        /// <summary>Nulo: ejercicio global. Con valor: ejercicio propio de ese gimnasio (Origen = Gimnasio). Etapa 9a-2.</summary>
        public Guid? GimnasioId { get; set; }

        /// <summary>Tipo normalizado de equipamiento (Etapa 9a-2). El texto Equipamiento se conserva sin cambios.</summary>
        public Guid? TipoEquipamientoId { get; set; }
        public TipoEquipamiento? TipoEquipamiento { get; set; }

        // Motor IA
        public Vector? Embedding { get; set; }
    }
}