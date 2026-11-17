using GymBro.Application.Abstractions;
using GymBro.Application.Services;
using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GymBro.Infrastructure.Persistence.Seeders
{
    /// <summary>
    /// Datos de demostración para probar todos los roles sin scripts ni SQL manual.
    /// Solo corre en Development y con DemoData:Habilitado = true. No toca nada si detecta datos de demo previos.
    /// La contraseña de las cuentas demo sale de DemoData:Password (user-secrets); sin ella no se siembra nada (Etapa 8c, T9).
    /// </summary>
    public static class DemoSeeder
    {
        public const string ClavePassword = "DemoData:Password";

        public const string NombreGimnasio = "GymBro Demo";
        public const string CodigoGimnasio = "DEMO01";

        private const string EmailDueno = "dueno.demo@gymbro.local";
        private const string EmailEntrenador = "entrenador.demo@gymbro.local";
        private const string EmailLucas = "lucas.perez@test.local";
        private const string EmailAna = "ana.gomez@test.local";
        private const string EmailMario = "mario.ruiz@test.local";
        private const string EmailSofia = "sofia.invitada@test.local";

        private const string DniLucas = "30111222";
        private const string DniAna = "33444555";
        private const string DniMario = "28999000";
        private const string DniSofia = "35222111";

        /// <returns>true si creó los datos de demostración.</returns>
        public static async Task<bool> SeedAsync(GymBroDbContext context, IHasheadorContrasena hasheador, string urlFront, string? password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                Console.WriteLine($"[DEMO] DemoData:Habilitado es verdadero pero falta {ClavePassword}: se omiten los datos de demostración. " +
                    $"Configurala con: dotnet user-secrets set \"{ClavePassword}\" \"<contraseña>\" --project GymBro.Api");
                return false;
            }

            try
            {
                var emails = new[] { EmailDueno, EmailEntrenador, EmailLucas, EmailAna, EmailMario, EmailSofia };
                var dnis = new[] { DniLucas, DniAna, DniMario, DniSofia };
                var yaExiste =
                    await context.Gimnasios.AnyAsync(g => g.Nombre == NombreGimnasio || g.Codigo == CodigoGimnasio) ||
                    await context.Usuarios.AnyAsync(u => emails.Contains(u.Email)) ||
                    await context.Socios.AnyAsync(s => (s.Dni != null && dnis.Contains(s.Dni)) || emails.Contains(s.Email));
                if (yaExiste)
                {
                    Console.WriteLine("[DEMO] Ya hay datos de demostración (gimnasio, cuentas o DNI): no se crea nada.");
                    return false;
                }

                var gimnasio = new Gimnasio { Nombre = NombreGimnasio, Codigo = CodigoGimnasio, RequiereAprobacionRutinas = true };

                var dueno = new Usuario
                {
                    Email = EmailDueno, PasswordHash = hasheador.Hash(password),
                    NombreCompleto = "Dueño Demo", Rol = RolUsuario.AdministradorGimnasio, GimnasioId = gimnasio.Id
                };
                var entrenador = new Usuario
                {
                    Email = EmailEntrenador, PasswordHash = hasheador.Hash(password),
                    NombreCompleto = "Entrenador Demo", Rol = RolUsuario.Entrenador, GimnasioId = gimnasio.Id
                };

                // Lucas: socio con cuenta, vinculado al gimnasio y asignado al entrenador.
                var lucas = NuevoSocio("Lucas", "Pérez", EmailLucas, DniLucas, "M", new DateTime(1995, 3, 14),
                    ObjetivoEntrenamiento.GananciaMuscular, NivelExperiencia.Intermedio, gimnasio.Id);
                // Ana: socia independiente (nunca se vincula).
                var ana = NuevoSocio("Ana", "Gómez", EmailAna, DniAna, "F", new DateTime(1998, 7, 2),
                    ObjetivoEntrenamiento.Mantenimiento, NivelExperiencia.Principiante, null);
                // Mario: socio independiente con una solicitud de vinculación pendiente para aprobar o rechazar.
                var mario = NuevoSocio("Mario", "Ruiz", EmailMario, DniMario, "M", new DateTime(1990, 11, 23),
                    ObjetivoEntrenamiento.PerdidaPeso, NivelExperiencia.Principiante, null);
                // Sofía: ficha del alta presencial, todavía sin cuenta (tiene una invitación para activar).
                var sofia = new Socio
                {
                    Id = Guid.NewGuid(), Nombre = "Sofía", Apellido = "Invitada", Email = EmailSofia, Dni = DniSofia,
                    GimnasioId = gimnasio.Id, Objetivo = ObjetivoEntrenamiento.Mantenimiento, Nivel = NivelExperiencia.Principiante
                };

                var tokenSofia = CodigosInvitacion.Generar();
                var invitacion = new InvitacionSocio
                {
                    GimnasioId = gimnasio.Id, SocioId = sofia.Id,
                    TokenHash = CodigosInvitacion.Hash(tokenSofia),
                    ExpiraUtc = DateTime.UtcNow.Add(InvitacionService.Vigencia)
                };

                context.AddRange(gimnasio, dueno, entrenador, lucas, ana, mario, sofia);
                context.AddRange(
                    CuentaDe(lucas, password, hasheador), CuentaDe(ana, password, hasheador), CuentaDe(mario, password, hasheador));
                context.Add(invitacion);
                context.Add(new SolicitudVinculacion
                {
                    SocioId = mario.Id, GimnasioId = gimnasio.Id, NombreSocio = mario.NombreCompleto, DniDeclarado = DniMario
                });
                context.Add(new AsignacionEntrenadorSocio { GimnasioId = gimnasio.Id, EntrenadorUsuarioId = entrenador.Id, SocioId = lucas.Id });

                // Rutina pendiente de revisión para Lucas, con ejercicios del catálogo (si ya fue cargado).
                var ejercicios = await context.Ejercicios.OrderBy(e => e.Nombre).Take(3).ToListAsync();
                if (ejercicios.Count > 0)
                {
                    var rutina = new PrescripcionEntrenamiento
                    {
                        SocioId = lucas.Id, Estado = EstadoPrescripcion.PendienteRevision,
                        OrigenGeneracion = "DEMO", Observaciones = "Rutina de demostración para probar la revisión."
                    };
                    var orden = 1;
                    foreach (var e in ejercicios)
                        rutina.EjerciciosPrescritos.Add(new PrescripcionEjercicio
                        {
                            EjercicioId = e.Id, Orden = orden++, SeriesObjetivo = 3,
                            RepeticionesMinimas = 8, RepeticionesMaximas = 12, RIRObjetivo = 2, DescansoObjetivoSegundos = 90
                        });
                    context.Add(rutina);
                }
                else
                {
                    Console.WriteLine("[DEMO] El catálogo de ejercicios está vacío: no se crea la rutina pendiente.");
                }

                await context.SaveChangesAsync();

                Console.WriteLine("========================================");
                Console.WriteLine($"[DEMO] Gimnasio '{NombreGimnasio}' creado. Código: {CodigoGimnasio}");
                Console.WriteLine($"[DEMO] Cuentas (contraseña: la de {ClavePassword}):");
                Console.WriteLine($"[DEMO] Dueño: {EmailDueno}");
                Console.WriteLine($"[DEMO] Personal Trainer: {EmailEntrenador}");
                Console.WriteLine($"[DEMO] Socios: {EmailLucas} (vinculado y asignado), {EmailAna} (independiente), {EmailMario} (solicitud pendiente)");
                Console.WriteLine($"[DEMO] Invitación de {EmailSofia}: {urlFront.TrimEnd('/')}/activar/{tokenSofia}");
                Console.WriteLine("========================================");
                return true;
            }
            catch (Exception ex)
            {
                // Los datos de demostración nunca deben impedir que la API arranque.
                Console.WriteLine($"[DEMO][ERROR] No se pudieron crear los datos de demostración: {ex.Message}");
                return false;
            }
        }

        private static Socio NuevoSocio(string nombre, string apellido, string email, string dni, string genero,
            DateTime nacimiento, ObjetivoEntrenamiento objetivo, NivelExperiencia nivel, Guid? gimnasioId) => new()
        {
            Id = Guid.NewGuid(), Nombre = nombre, Apellido = apellido, Email = email, Dni = dni, Genero = genero,
            FechaNacimiento = DateTime.SpecifyKind(nacimiento, DateTimeKind.Utc),
            Objetivo = objetivo, Nivel = nivel, GimnasioId = gimnasioId,
            ConsentimientoDatosSaludFecha = DateTime.UtcNow
        };

        // La cuenta del socio no lleva gimnasio (CK_Usuario_Rol_Alcance): la pertenencia vive en Socio.GimnasioId.
        private static Usuario CuentaDe(Socio socio, string password, IHasheadorContrasena hasheador) => new()
        {
            Email = socio.Email, PasswordHash = hasheador.Hash(password), NombreCompleto = socio.NombreCompleto,
            Rol = RolUsuario.Socio, SocioId = socio.Id
        };
    }
}
