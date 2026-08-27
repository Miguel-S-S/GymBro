using CsvHelper;
using CsvHelper.Configuration;
using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Infrastructure.Persistence.Seeders.Mappings;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using System.ComponentModel;
using System.Globalization;

namespace GymBro.Infrastructure.Persistence.Seeders
{
    public static class DataSeeder
    {
        public static async Task SeedAsync(GymBroDbContext context, string basePath)
        {
            ExcelPackage.License.SetNonCommercialPersonal("GymBro");

            Console.WriteLine("\n=== INICIANDO PROCESO ETL Y POBLAMIENTO DE BASE DE DATOS ===");

            // 1. Cargar Dosificaciones (series.csv)
            if (!await context.Dosificaciones.AnyAsync())
            {
                try
                {
                    Console.WriteLine("[INFO] Procesando series.csv...");
                    await CargarDosificaciones(context, Path.Combine(basePath, "series.csv"));
                    Console.WriteLine("[OK] Dosificaciones insertadas correctamente.");
                }
                catch (Exception ex) { Console.WriteLine($"[ERROR series.csv] {ex.Message}"); }
            }

            // 2. Cargar Matriz Clínica (gym_recommendation.xlsx)
            if (!await context.MatricesRecomendacion.AnyAsync())
            {
                try
                {
                    Console.WriteLine("[INFO] Procesando gym_recommendation.xlsx...");
                    await CargarMatrizExcel(context, Path.Combine(basePath, "gym_recommendation.xlsx"));
                    Console.WriteLine("[OK] Matriz de Recomendaciones insertada correctamente.");
                }
                catch (Exception ex) { Console.WriteLine($"[ERROR Excel] {ex.Message}"); }
            }

            // 3. Cargar Socios y Mediciones Físicas
            if (!await context.Socios.AnyAsync())
            {
                try
                {
                    Console.WriteLine("[INFO] Procesando mediciones_fisicas_maestro.csv...");
                    await CargarSociosYMediciones(context, Path.Combine(basePath, "mediciones_fisicas_maestro.csv"));
                    Console.WriteLine("[OK] Socios y Mediciones insertados correctamente.");
                }
                catch (Exception ex) { Console.WriteLine($"[ERROR Maestro Clínico] {ex.Message}"); }
            }

            // 4. Cargar Catálogos de Ejercicios
            if (!await context.Ejercicios.AnyAsync())
            {
                try
                {
                    Console.WriteLine("[INFO] Procesando catálogos de ejercicios...");
                    await CargarCatalogosDeEjercicios(context, basePath);
                    Console.WriteLine("[OK] Ejercicios de Musculación y Estiramientos insertados correctamente.");
                }
                catch (Exception ex) { Console.WriteLine($"[ERROR Ejercicios] {ex.Message}"); }
            }

            Console.WriteLine("=== PROCESO ETL FINALIZADO ===\n");
        }

        private static async Task CargarDosificaciones(GymBroDbContext context, string filePath)
        {
            if (!File.Exists(filePath)) throw new FileNotFoundException("Archivo no encontrado", filePath);

            var config = new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true };
            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, config);
            csv.Context.RegisterClassMap<DosificacionMap>();

            var records = csv.GetRecords<DosificacionVolumetrica>().ToList();
            await context.Dosificaciones.AddRangeAsync(records);
            await context.SaveChangesAsync();
        }

        private static async Task CargarMatrizExcel(GymBroDbContext context, string filePath)
        {
            if (!File.Exists(filePath)) throw new FileNotFoundException("Archivo Excel no encontrado", filePath);

            using var package = new ExcelPackage(new FileInfo(filePath));
            var worksheet = package.Workbook.Worksheets[0];
            int rowCount = worksheet.Dimension.Rows;
            var matrices = new List<MatrizRecomendacion>();

            for (int row = 2; row <= rowCount; row++)
            {
                matrices.Add(new MatrizRecomendacion
                {
                    Genero = worksheet.Cells[row, 2].Text ?? "O",
                    Edad = int.TryParse(worksheet.Cells[row, 3].Text, out int e) ? e : 0,
                    AplicaHipertension = (worksheet.Cells[row, 6].Text ?? "").Equals("Yes", StringComparison.OrdinalIgnoreCase),
                    AplicaDiabetes = (worksheet.Cells[row, 7].Text ?? "").Equals("Yes", StringComparison.OrdinalIgnoreCase),
                    CategoriaRutina = worksheet.Cells[row, 12].Text ?? "",
                    SugerenciaDieta = worksheet.Cells[row, 14].Text ?? "",
                    RecomendacionGeneral = worksheet.Cells[row, 15].Text ?? "",
                    Objetivo = Enum.TryParse<ObjetivoEntrenamiento>((worksheet.Cells[row, 10].Text ?? "").Replace(" ", ""), true, out var obj) ? obj : ObjetivoEntrenamiento.Mantenimiento,
                    CategoriaIMC = Enum.TryParse<CategoriaIMC>(worksheet.Cells[row, 9].Text, true, out var imc) ? imc : CategoriaIMC.Normal
                });
            }
            await context.MatricesRecomendacion.AddRangeAsync(matrices);
            await context.SaveChangesAsync();
        }

        private static async Task CargarSociosYMediciones(GymBroDbContext context, string filePath)
        {
            if (!File.Exists(filePath)) throw new FileNotFoundException("Archivo no encontrado", filePath);

            var config = new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true };
            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, config);
            csv.Context.RegisterClassMap<RegistroMaestroMap>();

            var records = csv.GetRecords<RegistroMaestroDto>().ToList();
            var socios = new List<Socio>();

            foreach (var row in records)
            {
                var nuevoSocio = new Socio
                {
                    Id = Guid.NewGuid(),
                    Genero = row.Gender ?? "O",
                    FechaNacimiento = DateTime.TryParse(row.Birth_Date, out DateTime fecha) ? DateTime.SpecifyKind(fecha, DateTimeKind.Utc) : DateTime.UtcNow.AddYears(-20),
                    Objetivo = ObjetivoEntrenamiento.Mantenimiento,
                    Nivel = NivelExperiencia.Intermedio
                };

                var medicion = new MedicionFisica
                {
                    Id = Guid.NewGuid(),
                    SocioId = nuevoSocio.Id,
                    PesoKg = row.Weight_kg,
                    AlturaCm = row.Height_cm,
                    PorcentajeGrasa = row.Fat_Percentage,
                    FrecuenciaCardiacaReposo = row.Resting_BPM,
                    PresionArterialMedia = row.MAP_mmHg,
                    CondicionMedica = row.health_condition ?? "Sin Patología Declarada",
                    TipoEntrenamiento = Enum.TryParse<TipoEntrenamiento>(row.Workout_Type, true, out var t) ? t : TipoEntrenamiento.Fuerza
                };

                nuevoSocio.HistorialFisico.Add(medicion);
                socios.Add(nuevoSocio);
            }
            await context.Socios.AddRangeAsync(socios);
            await context.SaveChangesAsync();
        }

        private static async Task CargarCatalogosDeEjercicios(GymBroDbContext context, string basePath)
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true };
            var todosLosEjercicios = new List<Ejercicio>();

            var rutaMusculacion = Path.Combine(basePath, "gym_exercise_dataset.csv");
            if (File.Exists(rutaMusculacion))
            {
                using var reader = new StreamReader(rutaMusculacion);
                using var csv = new CsvReader(reader, config);
                csv.Context.RegisterClassMap<EjercicioMusculacionMap>();
                todosLosEjercicios.AddRange(csv.GetRecords<Ejercicio>().ToList());
            }

            var rutaEstiramientos = Path.Combine(basePath, "stretch_exercise_dataset.csv");
            if (File.Exists(rutaEstiramientos))
            {
                using var reader = new StreamReader(rutaEstiramientos);
                using var csv = new CsvReader(reader, config);
                csv.Context.RegisterClassMap<EjercicioEstiramientoMap>();
                todosLosEjercicios.AddRange(csv.GetRecords<Ejercicio>().ToList());
            }

            if (todosLosEjercicios.Any())
            {
                await context.Ejercicios.AddRangeAsync(todosLosEjercicios);
                await context.SaveChangesAsync();
            }
        }
    }
}