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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.HasPostgresExtension("vector");

            modelBuilder.Entity<Socio>()
                .Property(s => s.Genero)
                .HasMaxLength(1);

            //para no mostrar los registros que tienen fecha de baja, se aplica un filtro global a las entidades
            modelBuilder.Entity<Socio>().HasQueryFilter(s => s.FechaBaja == null);
            modelBuilder.Entity<MedicionFisica>().HasQueryFilter(m => m.FechaBaja == null);
            modelBuilder.Entity<Ejercicio>().HasQueryFilter(e => e.FechaBaja == null);
            modelBuilder.Entity<SesionEntrenamiento>().HasQueryFilter(se => se.FechaBaja == null);

            // 3. Relación Socio -> MedicionFisica (Restricción de borrado)
            modelBuilder.Entity<Socio>()
                .HasMany(s => s.HistorialFisico) // Nombre de la colección en la entidad Socio
                .WithOne(m => m.Socio)           // Propiedad de navegación en MedicionFisica
                .HasForeignKey(m => m.SocioId)
                .OnDelete(DeleteBehavior.Restrict);

            // 4. Configuración de la tabla intermedia SesionEjercicio
            modelBuilder.Entity<SesionEjercicio>()
                .HasKey(se => new { se.SesionId, se.EjercicioId });

            modelBuilder.Entity<SesionEjercicio>()
                .HasOne(se => se.Sesion)
                .WithMany(s => s.EjerciciosEjecutados)
                .HasForeignKey(se => se.SesionId);

            modelBuilder.Entity<SesionEjercicio>()
                .HasOne(se => se.Ejercicio)
                .WithMany()
                .HasForeignKey(se => se.EjercicioId);

            // 5. Conversión de Enums a texto puro en la base de datos
            modelBuilder.Entity<Socio>()
                .Property(s => s.Objetivo)
                .HasConversion<string>();

            modelBuilder.Entity<Socio>()
                .Property(s => s.Nivel)
                .HasConversion<string>();

            modelBuilder.Entity<MatrizRecomendacion>()
                .Property(m => m.Objetivo)
                .HasConversion<string>();
        }

    }
}