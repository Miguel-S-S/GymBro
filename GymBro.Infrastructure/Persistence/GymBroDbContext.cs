using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace GymBro.Infrastructure.Persistence
{
    public class GymBroDbContext : DbContext
    {
        // Identidad del request: la leen los filtros globales de consulta (aislamiento multi-tenant).
        private readonly IUsuarioActual _actual;

        public GymBroDbContext(DbContextOptions<GymBroDbContext> options, IUsuarioActual actual) : base(options)
        {
            _actual = actual;
        }

        public DbSet<Gimnasio> Gimnasios { get; set; }
        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<AsignacionEntrenadorSocio> Asignaciones { get; set; }
        public DbSet<SolicitudVinculacion> SolicitudesVinculacion { get; set; }
        public DbSet<RevisionPrescripcion> RevisionesPrescripcion { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }

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

            // Aislamiento multi-tenant: un usuario de gimnasio solo ve los socios de su gimnasio; un socio solo se ve a sí mismo.
            modelBuilder.Entity<Socio>().HasQueryFilter(s => s.FechaBaja == null && (
                _actual.EsProcesoDeSistema
                || (_actual.GimnasioId != null && s.GimnasioId == _actual.GimnasioId)
                || (_actual.SocioId != null && s.Id == _actual.SocioId)));

            modelBuilder.Entity<MedicionFisica>().HasQueryFilter(m => m.FechaBaja == null && ( // using correct mapping below
                _actual.EsProcesoDeSistema
                || (_actual.GimnasioId != null && m.Socio.GimnasioId == _actual.GimnasioId)
                || (_actual.SocioId != null && m.SocioId == _actual.SocioId)));

            modelBuilder.Entity<Ejercicio>().HasQueryFilter(e => e.FechaBaja == null);
            modelBuilder.Entity<Ejercicio>().Property(e => e.Embedding).HasColumnType("vector(768)");

            modelBuilder.Entity<SesionEntrenamiento>().HasQueryFilter(se => se.FechaBaja == null && (
                _actual.EsProcesoDeSistema
                || (_actual.GimnasioId != null && se.Socio.GimnasioId == _actual.GimnasioId)
                || (_actual.SocioId != null && se.SocioId == _actual.SocioId)));

            modelBuilder.Entity<MatrizRecomendacion>().HasQueryFilter(m => m.FechaBaja == null);
            modelBuilder.Entity<DosificacionVolumetrica>().HasQueryFilter(d => d.FechaBaja == null);

            // 3. Relación Socio -> MedicionFisica (Restricción de borrado)
            modelBuilder.Entity<Socio>()
                .HasMany(s => s.HistorialFisico)
                .WithOne(m => m.Socio)
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

            modelBuilder.Entity<PrescripcionEntrenamiento>().HasQueryFilter(p => p.FechaBaja == null && (
                _actual.EsProcesoDeSistema
                || (_actual.GimnasioId != null && p.Socio.GimnasioId == _actual.GimnasioId)
                || (_actual.SocioId != null && p.SocioId == _actual.SocioId)));

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

            ConfigurarAccesoYTenants(modelBuilder);

            // 5. Conversión de Enums a texto puro en la base de datos
            modelBuilder.Entity<Socio>().Property(s => s.Objetivo).HasConversion<string>();
            modelBuilder.Entity<Socio>().Property(s => s.Nivel).HasConversion<string>();
            modelBuilder.Entity<MatrizRecomendacion>().Property(m => m.Objetivo).HasConversion<string>();
            modelBuilder.Entity<MatrizRecomendacion>().Property(m => m.CategoriaIMC).HasConversion<string>();
            modelBuilder.Entity<MedicionFisica>().Property(m => m.TipoEntrenamiento).HasConversion<string>();
            modelBuilder.Entity<PrescripcionEntrenamiento>().Property(p => p.Estado).HasConversion<string>();
        }

        /// <summary>Modelo de acceso (roles), multi-tenant, vinculación, asignación, revisión de rutinas y auditoría.</summary>
        private void ConfigurarAccesoYTenants(ModelBuilder modelBuilder)
        {
            // Gimnasio (tenant)
            modelBuilder.Entity<Gimnasio>(e =>
            {
                e.HasQueryFilter(g => g.FechaBaja == null && (
                    _actual.EsProcesoDeSistema
                    || _actual.Rol == RolUsuario.SuperAdministrador
                    || (_actual.GimnasioId != null && g.Id == _actual.GimnasioId)));
                e.Property(g => g.Nombre).HasMaxLength(150).IsRequired();
                e.Property(g => g.Codigo).HasMaxLength(12).IsRequired();
                e.HasIndex(g => g.Codigo).IsUnique();
            });

            modelBuilder.Entity<Socio>(e =>
            {
                e.Property(s => s.Dni).HasMaxLength(9);
                e.HasIndex(s => s.Dni).IsUnique(); // PostgreSQL permite múltiples NULL en un índice único
                e.HasOne(s => s.Gimnasio).WithMany(g => g.Socios)
                    .HasForeignKey(s => s.GimnasioId).OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(s => s.GimnasioId);
            });

            // Usuario (cuenta de acceso)
            modelBuilder.Entity<Usuario>(e =>
            {
                e.HasQueryFilter(u => u.FechaBaja == null && (
                    _actual.EsProcesoDeSistema
                    || _actual.Rol == RolUsuario.SuperAdministrador
                    || u.Id == _actual.UsuarioId
                    || (_actual.GimnasioId != null && u.GimnasioId == _actual.GimnasioId)));
                e.Property(u => u.Email).HasMaxLength(254).IsRequired();
                e.HasIndex(u => u.Email).IsUnique();
                e.Property(u => u.PasswordHash).HasMaxLength(256).IsRequired();
                e.Property(u => u.NombreCompleto).HasMaxLength(200).IsRequired();
                e.Property(u => u.Rol).HasConversion<string>().HasMaxLength(30);
                e.HasOne(u => u.Gimnasio).WithMany().HasForeignKey(u => u.GimnasioId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(u => u.Socio).WithMany().HasForeignKey(u => u.SocioId).OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(u => u.SocioId).IsUnique();
                e.HasIndex(u => u.GimnasioId);
                // Coherencia rol/tenant: el dueño y el trainer pertenecen a un gimnasio; el socio tiene ficha; el super no tiene gimnasio.
                e.ToTable(t => t.HasCheckConstraint("CK_Usuario_Rol_Alcance",
                    "(rol = 'SuperAdministrador' AND gimnasio_id IS NULL AND socio_id IS NULL) " +
                    "OR (rol IN ('AdministradorGimnasio','Entrenador') AND gimnasio_id IS NOT NULL AND socio_id IS NULL) " +
                    "OR (rol = 'Socio' AND socio_id IS NOT NULL AND gimnasio_id IS NULL)"));
            });

            // Asignación entrenador <-> socio
            modelBuilder.Entity<AsignacionEntrenadorSocio>(e =>
            {
                e.HasQueryFilter(a => a.FechaBaja == null && (
                    _actual.EsProcesoDeSistema
                    || (_actual.GimnasioId != null && a.GimnasioId == _actual.GimnasioId)));
                e.HasOne<Gimnasio>().WithMany().HasForeignKey(a => a.GimnasioId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(a => a.EntrenadorUsuario).WithMany().HasForeignKey(a => a.EntrenadorUsuarioId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(a => a.Socio).WithMany().HasForeignKey(a => a.SocioId).OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(a => new { a.EntrenadorUsuarioId, a.SocioId }).IsUnique();
            });

            // Solicitud de vinculación socio -> gimnasio
            modelBuilder.Entity<SolicitudVinculacion>(e =>
            {
                e.HasQueryFilter(s => s.FechaBaja == null && (
                    _actual.EsProcesoDeSistema
                    || (_actual.GimnasioId != null && s.GimnasioId == _actual.GimnasioId)
                    || (_actual.SocioId != null && s.SocioId == _actual.SocioId)));
                e.Property(s => s.Estado).HasConversion<string>().HasMaxLength(20);
                e.Property(s => s.DniDeclarado).HasMaxLength(9).IsRequired();
                e.Property(s => s.NombreSocio).HasMaxLength(200).IsRequired();
                e.Property(s => s.Motivo).HasMaxLength(500);
                e.HasOne(s => s.Socio).WithMany().HasForeignKey(s => s.SocioId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(s => s.Gimnasio).WithMany().HasForeignKey(s => s.GimnasioId).OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(s => new { s.GimnasioId, s.Estado });
                // Un socio no puede tener dos solicitudes pendientes al mismo gimnasio.
                e.HasIndex(s => new { s.SocioId, s.GimnasioId }).IsUnique().HasFilter("estado = 'Pendiente'");
            });

            // Traza de revisión humana de rutinas
            modelBuilder.Entity<RevisionPrescripcion>(e =>
            {
                e.Property(r => r.Decision).HasConversion<string>().HasMaxLength(20);
                e.Property(r => r.Comentario).HasMaxLength(500);
                e.HasOne(r => r.PrescripcionEntrenamiento).WithMany(p => p.Revisiones)
                    .HasForeignKey(r => r.PrescripcionEntrenamientoId).OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(r => r.PrescripcionEntrenamientoId);
            });

            // Auditoría (append-only)
            modelBuilder.Entity<AuditLog>(e =>
            {
                e.Property(a => a.Rol).HasMaxLength(30);
                e.Property(a => a.Accion).HasMaxLength(60).IsRequired();
                e.Property(a => a.Entidad).HasMaxLength(60).IsRequired();
                e.Property(a => a.EntidadId).HasMaxLength(64);
                e.Property(a => a.Detalle).HasMaxLength(500);
                e.HasIndex(a => a.Fecha);
                e.HasIndex(a => a.GimnasioId);
            });
        }
    }
}