using GymBro.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GymBro.Infrastructure.Persistence
{
    public class GymBroDbContext : DbContext
    {
        public GymBroDbContext(DbContextOptions<GymBroDbContext> options) : base(options) { }

        public DbSet<Socio> Socios { get; set; }
        public DbSet<MedicionFisica> MedicionesFisicas { get; set; }
        public DbSet<Ejercicio> Ejercicios { get; set; }
        public DbSet<SesionEntrenamiento> Sesiones { get; set; }
        public DbSet<SesionEjercicio> SesionesEjercicios { get; set; }
        public DbSet<MatrizRecomendacion> MatricesRecomendacion { get; set; }
        public DbSet<DosificacionVolumetrica> Dosificaciones { get; set; }
        public DbSet<PrescripcionEntrenamiento> PrescripcionesEntrenamiento { get; set; }
        public DbSet<PrescripcionEjercicio> PrescripcionesEjercicios { get; set; }
        public DbSet<SerieRealizada> SeriesRealizadas { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.HasPostgresExtension("vector");

            modelBuilder.Entity<Socio>()
                .Property(s => s.Genero)
                .HasMaxLength(1);
            modelBuilder.Entity<MatrizRecomendacion>().Property(m => m.Genero).HasMaxLength(1);


            //para no mostrar los registros que tienen fecha de baja, se aplica un filtro global a las entidades
            modelBuilder.Entity<Socio>().HasQueryFilter(s => s.FechaBaja == null);
            modelBuilder.Entity<MedicionFisica>().HasQueryFilter(m => m.FechaBaja == null);
            modelBuilder.Entity<Ejercicio>().HasQueryFilter(e => e.FechaBaja == null);
            modelBuilder.Entity<Ejercicio>().Property(e => e.Embedding).HasColumnType("vector(768)");
            modelBuilder.Entity<SesionEntrenamiento>().HasQueryFilter(se => se.FechaBaja == null);
            modelBuilder.Entity<MatrizRecomendacion>().HasQueryFilter(m => m.FechaBaja == null);
            modelBuilder.Entity<DosificacionVolumetrica>().HasQueryFilter(d => d.FechaBaja == null);

            // 3. Relación Socio -> MedicionFisica (Restricción de borrado)
            modelBuilder.Entity<Socio>()
                .HasMany(s => s.HistorialFisico) // Nombre de la colección en la entidad Socio
                .WithOne(m => m.Socio)           // Propiedad de navegación en MedicionFisica
                .HasForeignKey(m => m.SocioId)
                .OnDelete(DeleteBehavior.Restrict);

            // 4. Configuración de SesionEjercicio
            modelBuilder.Entity<SesionEjercicio>()
                .HasOne(se => se.Sesion)
                .WithMany(s => s.EjerciciosEjecutados)
                .HasForeignKey(se => se.SesionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SesionEjercicio>()
                .HasOne(se => se.Ejercicio)
                .WithMany()
                .HasForeignKey(se => se.EjercicioId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SesionEjercicio>().HasQueryFilter(se => se.FechaBaja == null);
            modelBuilder.Entity<SesionEjercicio>().Property(se => se.Observaciones).HasMaxLength(500);
            modelBuilder.Entity<SesionEjercicio>().ToTable(t => t.HasCheckConstraint("CK_SesionEjercicio_Orden", "orden > 0"));

            // Configuración de SerieRealizada
            modelBuilder.Entity<SerieRealizada>().HasQueryFilter(sr => sr.FechaBaja == null);

            modelBuilder.Entity<SerieRealizada>()
                .HasOne(sr => sr.SesionEjercicio)
                .WithMany(se => se.SeriesRealizadas)
                .HasForeignKey(sr => sr.SesionEjercicioId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SerieRealizada>()
                .ToTable(t => {
                    t.HasCheckConstraint("CK_SerieRealizada_OrdenSerie", "orden_serie > 0");
                    t.HasCheckConstraint("CK_SerieRealizada_PesoLevantadoKg", "peso_levantado_kg >= 0");
                    t.HasCheckConstraint("CK_SerieRealizada_Repeticiones", "repeticiones > 0");
                    t.HasCheckConstraint("CK_SerieRealizada_RIR", "rir IS NULL OR (rir >= 0 AND rir <= 5)");
                    t.HasCheckConstraint("CK_SerieRealizada_TiempoDescansoSegundos", "tiempo_descanso_segundos IS NULL OR tiempo_descanso_segundos >= 0");
                });

            modelBuilder.Entity<PrescripcionEntrenamiento>().HasQueryFilter(p => p.FechaBaja == null);
            modelBuilder.Entity<PrescripcionEjercicio>().HasQueryFilter(p => p.FechaBaja == null);

            modelBuilder.Entity<PrescripcionEntrenamiento>()
                .Property(p => p.OrigenGeneracion).HasMaxLength(50);
            modelBuilder.Entity<PrescripcionEntrenamiento>()
                .Property(p => p.Observaciones).HasMaxLength(500);
            modelBuilder.Entity<PrescripcionEjercicio>()
                .Property(p => p.Observaciones).HasMaxLength(500);

            modelBuilder.Entity<Socio>()
                .HasMany(s => s.Prescripciones)
                .WithOne(p => p.Socio)
                .HasForeignKey(p => p.SocioId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PrescripcionEntrenamiento>()
                .HasMany(p => p.Sesiones)
                .WithOne(s => s.PrescripcionEntrenamiento)
                .HasForeignKey(s => s.PrescripcionEntrenamientoId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<PrescripcionEntrenamiento>()
                .HasMany(p => p.EjerciciosPrescritos)
                .WithOne(pe => pe.PrescripcionEntrenamiento)
                .HasForeignKey(pe => pe.PrescripcionEntrenamientoId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PrescripcionEjercicio>()
                .HasOne(pe => pe.Ejercicio)
                .WithMany()
                .HasForeignKey(pe => pe.EjercicioId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PrescripcionEjercicio>()
                .HasIndex(pe => new { pe.PrescripcionEntrenamientoId, pe.Orden })
                .IsUnique();

            modelBuilder.Entity<PrescripcionEjercicio>()
                .ToTable(t => {
                    t.HasCheckConstraint("CK_PrescripcionEjercicio_SeriesObjetivo", "series_objetivo > 0");
                    t.HasCheckConstraint("CK_PrescripcionEjercicio_RepeticionesMinimas", "repeticiones_minimas > 0");
                    t.HasCheckConstraint("CK_PrescripcionEjercicio_RepeticionesMaximas", "repeticiones_maximas >= repeticiones_minimas");
                    t.HasCheckConstraint("CK_PrescripcionEjercicio_RIRObjetivo", "rir_objetivo IS NULL OR (rir_objetivo >= 0 AND rir_objetivo <= 5)");
                    t.HasCheckConstraint("CK_PrescripcionEjercicio_DescansoObjetivoSegundos", "descanso_objetivo_segundos > 0");
                    t.HasCheckConstraint("CK_PrescripcionEjercicio_Orden", "orden > 0");
                });

            // 5. Conversión de Enums a texto puro en la base de datos
            // Conversiones de Enums a texto puro
            modelBuilder.Entity<Socio>().Property(s => s.Objetivo).HasConversion<string>();
            modelBuilder.Entity<Socio>().Property(s => s.Nivel).HasConversion<string>();
            modelBuilder.Entity<MatrizRecomendacion>().Property(m => m.Objetivo).HasConversion<string>();
            modelBuilder.Entity<MatrizRecomendacion>().Property(m => m.CategoriaIMC).HasConversion<string>();
            modelBuilder.Entity<MedicionFisica>().Property(m => m.TipoEntrenamiento).HasConversion<string>();
            modelBuilder.Entity<PrescripcionEntrenamiento>().Property(p => p.Estado).HasConversion<string>();
        }

    }
}