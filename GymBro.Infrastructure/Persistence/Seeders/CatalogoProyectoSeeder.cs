using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GymBro.Infrastructure.Persistence.Seeders
{
    /// <summary>
    /// Catálogo propio del proyecto (Etapa 9a-1): ejercicios de Core y su dosificación, que no dependen de los CSV de Kaggle.
    /// Se invoca al arrancar en todos los entornos, después de MigrateAsync y de DataSeeder (decisión A, 17/11/2026).
    /// Idempotente: inserta solo las filas cuyo Id fijo todavía no existe (incluidas las dadas de baja), así que nunca
    /// sobrescribe ni reactiva lo que el Dueño o el autor hayan editado o dado de baja.
    ///
    /// ADVERTENCIA: los textos (músculo principal, preparación y ejecución), la dificultad y los rangos de dosificación son
    /// BORRADORES PROVISIONALES, sin validar por un profesional. Son decisiones de diseño, no criterios clínicos. Antes de
    /// usarlos con personas reales debe revisarlos un Personal Trainer.
    ///
    /// Los ejercicios se insertan con Embedding = null: la búsqueda semántica no los encuentra hasta ejecutar
    /// GymBro.IA/generador_embeddings.py (procesa los ejercicios con embedding IS NULL).
    /// </summary>
    public static class CatalogoProyectoSeeder
    {
        public const string Zona = "Core";

        // Valores exactos ya existentes en ejercicios.equipamiento (verificados el 17/11/2026).
        private const string PesoCorporal = "Body Weight";
        private const string Cable = "Cable";

        public static readonly Guid DosisCoreRepeticionesId = Guid.Parse("678170ad-3a31-4389-a781-d9e0b4232696");
        public static readonly Guid DosisCoreSegundosId = Guid.Parse("013c3b58-0969-45df-a4d4-3a5e68ddc1b4");

        /// <summary>Ids de las filas de dosificación propias del proyecto. DataSeeder las ignora al decidir si carga series.csv.</summary>
        public static readonly Guid[] IdsDosificacionesProyecto = { DosisCoreRepeticionesId, DosisCoreSegundosId };

        /// <summary>Ejercicios de Core del proyecto (Id fijo, Origen = Proyecto, Zona = Core).</summary>
        public static IReadOnlyList<Ejercicio> Ejercicios() => new List<Ejercicio>
        {
            Nuevo("13e90ee5-9fb4-47bf-9abc-1277eae966d1", "Plancha", PesoCorporal, TipoMedida.Segundos, 2,
                "Recto abdominal y transverso del abdomen", "Oblicuos", "Glúteos, hombros",
                "Apoyar los antebrazos en el suelo con los codos debajo de los hombros. Extender las piernas y apoyar las puntas de los pies.",
                "Elevar la cadera hasta alinear hombros, cadera y tobillos. Sostener la posición durante el tiempo indicado, respirando con normalidad."),
            Nuevo("f6bab470-e569-44c2-b48a-f0127ad3b9e9", "Crunch abdominal", PesoCorporal, TipoMedida.Repeticiones, 1,
                "Recto abdominal", "Oblicuos", "Ninguno",
                "Acostarse boca arriba con las rodillas flexionadas y los pies apoyados en el suelo. Cruzar los brazos sobre el pecho o llevar las manos a los costados de la cabeza.",
                "Elevar los hombros del suelo flexionando el tronco. Volver de forma controlada a la posición inicial."),
            Nuevo("b75a84d9-e7ae-4ce4-b454-93f16efb131b", "Elevación de piernas", PesoCorporal, TipoMedida.Repeticiones, 2,
                "Recto abdominal", "Flexores de cadera", "Oblicuos",
                "Acostarse boca arriba con las piernas extendidas y los brazos a los costados del cuerpo.",
                "Elevar las piernas extendidas hasta formar un ángulo de unos 90 grados con el tronco. Bajarlas de forma controlada sin apoyarlas en el suelo."),
            Nuevo("b49dd356-9c0f-4402-81ac-df6a60050be7", "Bicicleta (cross bicycles)", PesoCorporal, TipoMedida.Repeticiones, 2,
                "Oblicuos", "Recto abdominal", "Flexores de cadera",
                "Acostarse boca arriba con las manos a los costados de la cabeza y las piernas elevadas, con las rodillas flexionadas.",
                "Acercar un codo a la rodilla opuesta mientras se extiende la otra pierna. Alternar los lados de forma continua y controlada."),
            Nuevo("a76e0fba-26ab-4a71-8889-3d1b2cf02dcb", "Mountain climber", PesoCorporal, TipoMedida.Repeticiones, 3,
                "Recto abdominal", "Flexores de cadera, hombros", "Oblicuos",
                "Colocarse en posición de plancha alta con las manos debajo de los hombros y el cuerpo alineado.",
                "Llevar una rodilla hacia el pecho y volver a extenderla. Alternar las piernas sin elevar la cadera."),
            Nuevo("cde60328-baa0-4942-9e9d-f1dc6578a092", "Tijera de piernas", PesoCorporal, TipoMedida.Repeticiones, 2,
                "Recto abdominal", "Flexores de cadera", "Oblicuos",
                "Acostarse boca arriba con las piernas extendidas y las manos debajo de la cadera o a los costados.",
                "Elevar las piernas unos centímetros del suelo y cruzarlas alternadamente una sobre otra. Mantener la zona lumbar apoyada."),
            Nuevo("f5854654-9b06-45d2-8b26-79074712d9d7", "Bote (boat)", PesoCorporal, TipoMedida.Segundos, 2,
                "Recto abdominal", "Flexores de cadera", "Oblicuos, erectores espinales",
                "Sentarse en el suelo con las rodillas flexionadas y los pies apoyados.",
                "Inclinar el tronco hacia atrás y elevar los pies hasta equilibrarse sobre los glúteos, con la espalda recta. Sostener la posición durante el tiempo indicado."),
            Nuevo("735b2510-d182-41ba-967f-857401b336a5", "Crunch en polea", Cable, TipoMedida.Repeticiones, 2,
                "Recto abdominal", "Oblicuos", "Ninguno",
                "Arrodillarse frente a una polea alta y tomar la cuerda con ambas manos a los costados de la cabeza.",
                "Flexionar el tronco llevando los codos hacia los muslos, sin mover la cadera. Volver de forma controlada a la posición inicial."),
        };

        /// <summary>Dosificación de Core (propuesta de diseño, no criterio clínico).</summary>
        public static IReadOnlyList<DosificacionVolumetrica> Dosificaciones() => new List<DosificacionVolumetrica>
        {
            // Repeticiones: 3-4 series x 10-15 (igual que las filas de abdominales del dataset).
            new()
            {
                Id = DosisCoreRepeticionesId, ParteCuerpo = Zona, SubzonaObjetivo = "General", ZonaObjetivo = Zona,
                SeriesMinimas = 3, SeriesMaximas = 4, RepeticionesMinimas = 10, RepeticionesMaximas = 15,
                TipoMedida = TipoMedida.Repeticiones,
            },
            // Segundos: 2-3 series x 20-40 s (los campos de repeticiones guardan segundos).
            new()
            {
                Id = DosisCoreSegundosId, ParteCuerpo = Zona, SubzonaObjetivo = "General", ZonaObjetivo = Zona,
                SeriesMinimas = 2, SeriesMaximas = 3, RepeticionesMinimas = 20, RepeticionesMaximas = 40,
                TipoMedida = TipoMedida.Segundos,
            },
        };

        /// <summary>Inserta lo que falta. Devuelve cuántas filas agregó (ejercicios, dosificaciones).</summary>
        public static async Task<(int Ejercicios, int Dosificaciones)> SeedAsync(GymBroDbContext context)
        {
            var ejercicios = Ejercicios();
            var idsEjercicios = ejercicios.Select(e => e.Id).ToArray();
            var existentes = await context.Ejercicios.IgnoreQueryFilters()
                .Where(e => idsEjercicios.Contains(e.Id)).Select(e => e.Id).ToListAsync();
            var nuevosEjercicios = ejercicios.Where(e => !existentes.Contains(e.Id)).ToList();

            var dosisExistentes = await context.Dosificaciones.IgnoreQueryFilters()
                .Where(d => IdsDosificacionesProyecto.Contains(d.Id)).Select(d => d.Id).ToListAsync();
            var nuevasDosis = Dosificaciones().Where(d => !dosisExistentes.Contains(d.Id)).ToList();

            if (nuevosEjercicios.Count == 0 && nuevasDosis.Count == 0) return (0, 0);

            context.Ejercicios.AddRange(nuevosEjercicios);
            context.Dosificaciones.AddRange(nuevasDosis);
            await context.SaveChangesAsync();
            Console.WriteLine($"[OK] Catálogo del proyecto: {nuevosEjercicios.Count} ejercicios y {nuevasDosis.Count} dosificaciones de Core agregados.");
            return (nuevosEjercicios.Count, nuevasDosis.Count);
        }

        private static Ejercicio Nuevo(string id, string nombre, string equipamiento, TipoMedida medida, int dificultad,
            string musculo, string sinergistas, string estabilizadores, string preparacion, string ejecucion) => new()
        {
            Id = Guid.Parse(id),
            Nombre = nombre,
            Equipamiento = equipamiento,
            ZonaObjetivo = Zona,
            MusculoPrincipal = musculo,
            Sinergistas = sinergistas,
            Estabilizadores = estabilizadores,
            Preparacion = preparacion,
            Ejecucion = ejecucion,
            Dificultad = dificultad,
            TipoMedida = medida,
            Origen = OrigenEjercicio.Proyecto,
            Embedding = null,
        };
    }
}
