using System.Net;
using System.Text;
using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using Xunit;

namespace GymBro.Tests.Integration
{
    /// <summary>
    /// Etapa 8c, T4: matriz rol × endpoint contra la API real (Postgres descartable).
    /// Cada celda indica el código esperado y el atributo o servicio que lo justifica.
    /// Criterio (T7): 404 si el socio no es visible para quien llama; 403 solo si el recurso es visible y el rol no puede actuar.
    /// Escenario SINTÉTICO de prueba:
    ///   Gimnasio 1: Dueño, Personal Trainer (asignado al socio A), socio A (con cuenta) y socio B (sin trainer).
    ///   Gimnasio 2: socio C. Administrador de la Plataforma sin gimnasio.
    /// Las escrituras solo se prueban en las celdas que se rechazan antes de ejecutar la acción (401/403),
    /// para no producir efectos; el flujo permitido de escritura lo cubren SesionesAislamientoTests.
    /// </summary>
    public class MatrizAutorizacionTests : IClassFixture<MatrizAutorizacionTests.Escenario>
    {
        public const string Anonimo = "Anonimo";
        public const string SuperAdmin = nameof(RolUsuario.SuperAdministrador);
        public const string Dueno = nameof(RolUsuario.AdministradorGimnasio);
        public const string Trainer = nameof(RolUsuario.Entrenador);
        public const string Socio = nameof(RolUsuario.Socio);

        private const string AttrSocio = "SocioController: [Authorize(AdministradorGimnasio,Entrenador,Socio)]";
        private const string AttrSocioDueno = "SocioController: [Authorize(Roles = AdministradorGimnasio)] en la acción";
        private const string AttrPrescripciones = "PrescripcionesController: [Authorize(AdministradorGimnasio,Entrenador,Socio)]";
        private const string AttrRevisiones = "RevisionesController: [Authorize(Roles = PersonalGimnasio)]";
        private const string AttrSesiones = "SesionesController: [Authorize(AdministradorGimnasio,Entrenador,Socio)]";
        private const string AttrMedicion = "MedicionController: [Authorize(AdministradorGimnasio,Entrenador,Socio)]";
        private const string AttrVincSocio = "VinculacionesController: [Authorize(Roles = Socio)]";
        private const string AttrVincDueno = "VinculacionesController: [Authorize(Roles = AdministradorGimnasio)]";
        private const string AttrPersonal = "PersonalController: [Authorize(Roles = AdministradorGimnasio)]";
        private const string AttrGimnasios = "GimnasiosController: [Authorize(Roles = SuperAdministrador)]";
        private const string AttrMiGimnasio = "MiGimnasioController: [Authorize(Roles = AdministradorGimnasio)]";
        private const string AttrAuditoria = "AuditoriaController: [Authorize(Roles = SuperAdministrador)]";
        private const string Fallback = "Program.cs: FallbackPolicy RequireAuthenticatedUser / JwtBearer";
        private const string AccesoSocio = "SocioAccessService.AsegurarAccesoAsync";
        private const string Tenant = "filtro de tenant de GymBroDbContext (otro gimnasio)";
        private const string AttrBienestarSocio = "BienestarController: [Authorize(Roles = Socio)] en la acción";
        private const string AttrBienestarHistorial = "BienestarController.Historial: [Authorize(AdministradorGimnasio,Entrenador,Socio)]";
        private const string NoVisible ="SocioAccessService: socio no visible -> RecursoNoEncontrado (T7)";

        public static TheoryData<string, string, string, int, string> Celdas()
        {
            var d = new TheoryData<string, string, string, int, string>();
            void Fila(string metodo, string ruta, string justif, params (string Rol, int Codigo, string? Nota)[] celdas)
            {
                foreach (var (rol, codigo, nota) in celdas) d.Add(rol, metodo, ruta, codigo, nota ?? justif);
            }

            // ---- Socio ----
            Fila("GET", "/api/Socio", AttrSocio,
                (Anonimo, 401, Fallback), (SuperAdmin, 403, null), (Dueno, 200, null), (Trainer, 200, null), (Socio, 200, null));
            Fila("GET", "/api/Socio/{A}", AttrSocio,
                (Anonimo, 401, Fallback), (SuperAdmin, 403, null), (Dueno, 200, null), (Trainer, 200, AccesoSocio + " (asignado)"), (Socio, 200, AccesoSocio + " (propio)"));
            // T7: socio no visible para quien llama -> 404 (no se revela que existe).
            Fila("GET", "/api/Socio/{B}", AccesoSocio,
                (Trainer, 404, NoVisible + " (no asignado)"), (Socio, 404, NoVisible + " (ajeno)"), (Dueno, 200, AccesoSocio + " (mismo gimnasio)"));
            Fila("GET", "/api/Socio/{C}", Tenant, (Dueno, 404, Tenant + " + " + NoVisible + " (ExistsAsync = false)"));
            Fila("POST", "/api/Socio", AttrSocioDueno,
                (Anonimo, 401, Fallback), (SuperAdmin, 403, null), (Trainer, 403, null), (Socio, 403, null));
            Fila("PUT", "/api/Socio/{A}", AttrSocioDueno,
                (Anonimo, 401, Fallback), (SuperAdmin, 403, null), (Trainer, 403, null), (Socio, 403, null));
            Fila("DELETE", "/api/Socio/{A}", AttrSocioDueno,
                (Anonimo, 401, Fallback), (SuperAdmin, 403, null), (Trainer, 403, null), (Socio, 403, null));

            // ---- Prescripciones ----
            Fila("GET", "/api/Socio/{A}/prescripciones", AttrPrescripciones,
                (Anonimo, 401, Fallback), (SuperAdmin, 403, null), (Dueno, 200, null), (Trainer, 200, AccesoSocio), (Socio, 200, AccesoSocio));
            Fila("GET", "/api/Socio/{B}/prescripciones", NoVisible, (Trainer, 404, null), (Socio, 404, null));
            Fila("GET", "/api/Socio/{C}/prescripciones", NoVisible, (Dueno, 404, null));
            Fila("POST", "/api/Socio/{A}/prescripciones/baseline", AttrPrescripciones, (Anonimo, 401, Fallback), (SuperAdmin, 403, null));
            // Para la cuenta de Socio, PrescripcionBaselineService.GenerarAsync ya no encuentra a B (filtro de tenant).
            Fila("POST", "/api/Socio/{B}/prescripciones/baseline", NoVisible,
                (Trainer, 404, null), (Socio, 404, "PrescripcionBaselineService.GenerarAsync: RecursoNoEncontrado (filtro de tenant del Socio)"));

            // ---- Revisiones ----
            Fila("GET", "/api/revisiones/pendientes", AttrRevisiones,
                (Anonimo, 401, Fallback), (SuperAdmin, 403, null), (Dueno, 200, null), (Trainer, 200, null), (Socio, 403, null));
            Fila("POST", "/api/revisiones/{X}", AttrRevisiones, (Anonimo, 401, Fallback), (SuperAdmin, 403, null), (Socio, 403, null));

            // ---- Sesiones ----
            Fila("GET", "/api/Sesiones?socioId={A}", AttrSesiones,
                (Anonimo, 401, Fallback), (SuperAdmin, 403, null), (Dueno, 200, AccesoSocio), (Trainer, 200, AccesoSocio), (Socio, 200, AccesoSocio));
            Fila("GET", "/api/Sesiones?socioId={B}", NoVisible, (Trainer, 404, null), (Socio, 404, null));
            Fila("GET", "/api/Sesiones?socioId={C}", NoVisible, (Dueno, 404, null));
            Fila("POST", "/api/Sesiones", AttrSesiones, (Anonimo, 401, Fallback), (SuperAdmin, 403, null));

            // ---- Medición ----
            Fila("GET", "/api/Medicion?socioId={A}", AttrMedicion,
                (Anonimo, 401, Fallback), (SuperAdmin, 403, null), (Dueno, 200, AccesoSocio), (Trainer, 200, AccesoSocio), (Socio, 200, AccesoSocio));
            Fila("GET", "/api/Medicion?socioId={B}", NoVisible, (Trainer, 404, null), (Socio, 404, null));
            Fila("GET", "/api/Medicion?socioId={C}", NoVisible, (Dueno, 404, null));
            Fila("POST", "/api/Medicion", AttrMedicion, (Anonimo, 401, Fallback), (SuperAdmin, 403, null));

            // ---- Vinculaciones ----
            Fila("GET", "/api/vinculaciones/mias", AttrVincSocio,
                (Anonimo, 401, Fallback), (SuperAdmin, 403, null), (Dueno, 403, null), (Trainer, 403, null), (Socio, 200, null));
            Fila("POST", "/api/vinculaciones", AttrVincSocio, (Anonimo, 401, Fallback), (SuperAdmin, 403, null), (Dueno, 403, null), (Trainer, 403, null));
            Fila("GET", "/api/vinculaciones/pendientes", AttrVincDueno,
                (Anonimo, 401, Fallback), (SuperAdmin, 403, null), (Dueno, 200, null), (Trainer, 403, null), (Socio, 403, null));
            Fila("POST", "/api/vinculaciones/{X}/resolver", AttrVincDueno, (Anonimo, 401, Fallback), (SuperAdmin, 403, null), (Trainer, 403, null), (Socio, 403, null));

            // ---- Personal ----
            Fila("GET", "/api/personal/entrenadores", AttrPersonal,
                (Anonimo, 401, Fallback), (SuperAdmin, 403, null), (Dueno, 200, null), (Trainer, 403, null), (Socio, 403, null));
            Fila("POST", "/api/personal/entrenadores", AttrPersonal, (Anonimo, 401, Fallback), (SuperAdmin, 403, null), (Trainer, 403, null), (Socio, 403, null));
            Fila("POST", "/api/personal/entrenadores/{X}/socios", AttrPersonal, (Anonimo, 401, Fallback), (SuperAdmin, 403, null), (Trainer, 403, null), (Socio, 403, null));

            // ---- Gimnasios (plataforma) y Mi gimnasio (Dueño) ----
            Fila("GET", "/api/gimnasios", AttrGimnasios,
                (Anonimo, 401, Fallback), (SuperAdmin, 200, null), (Dueno, 403, null), (Trainer, 403, null), (Socio, 403, null));
            Fila("POST", "/api/gimnasios", AttrGimnasios, (Anonimo, 401, Fallback), (Dueno, 403, null), (Trainer, 403, null), (Socio, 403, null));
            Fila("PUT", "/api/gimnasios/{X}/estado", AttrGimnasios, (Anonimo, 401, Fallback), (Dueno, 403, null), (Trainer, 403, null), (Socio, 403, null));
            Fila("GET", "/api/mi-gimnasio", AttrMiGimnasio,
                (Anonimo, 401, Fallback), (SuperAdmin, 403, null), (Dueno, 200, null), (Trainer, 403, null), (Socio, 403, null));
            Fila("PUT", "/api/mi-gimnasio/politica-revision", AttrMiGimnasio, (Anonimo, 401, Fallback), (SuperAdmin, 403, null), (Trainer, 403, null), (Socio, 403, null));

            // ---- Catálogo de ejercicios (sin datos de salud; T8: también para el Administrador de la Plataforma) ----
            Fila("GET", "/api/Ejercicios", "EjerciciosController: sin [Authorize] propio -> FallbackPolicy (usuario autenticado)",
                (Anonimo, 401, Fallback), (SuperAdmin, 200, null), (Dueno, 200, null), (Trainer, 200, null), (Socio, 200, null));
            Fila("GET", "/api/Ejercicios/zonas", "EjerciciosController: FallbackPolicy (usuario autenticado)",
                (Anonimo, 401, Fallback), (SuperAdmin, 200, null));

            // ---- Bienestar diario (Etapa 8b; datos de salud: el Administrador de la Plataforma no accede) ----
            Fila("GET", "/api/bienestar/hoy", AttrBienestarSocio,
                (Anonimo, 401, Fallback), (SuperAdmin, 403, null), (Dueno, 403, null), (Trainer, 403, null), (Socio, 200, null));
            // Solo celdas rechazadas: el PUT permitido del Socio lo cubre BienestarTests (sin efectos acá).
            Fila("PUT", "/api/bienestar/{HOY}", AttrBienestarSocio,
                (Anonimo, 401, Fallback), (SuperAdmin, 403, null), (Dueno, 403, null), (Trainer, 403, null));
            Fila("GET", "/api/bienestar?socioId={A}", AttrBienestarHistorial,
                (Anonimo, 401, Fallback), (SuperAdmin, 403, null), (Dueno, 200, AccesoSocio), (Trainer, 200, AccesoSocio + " (asignado)"), (Socio, 200, AccesoSocio + " (propio)"));
            Fila("GET", "/api/bienestar?socioId={B}", NoVisible, (Trainer, 404, NoVisible + " (no asignado)"), (Socio, 404, NoVisible + " (ajeno)"));
            Fila("GET", "/api/bienestar?socioId={C}", NoVisible, (Dueno, 404, Tenant + " + " + NoVisible));

            // ---- Auditoría ----
            Fila("GET", "/api/auditoria", AttrAuditoria,
                (Anonimo, 401, Fallback), (SuperAdmin, 200, null), (Dueno, 403, null), (Trainer, 403, null), (Socio, 403, null));

            return d;
        }

        private readonly Escenario _esc;
        public MatrizAutorizacionTests(Escenario esc) { _esc = esc; }

        [Theory]
        [MemberData(nameof(Celdas))]
        public async Task Celda(string rol, string metodo, string ruta, int esperado, string justificacion)
        {
            var url = ruta.Replace("{A}", _esc.SocioA.Id.ToString()).Replace("{B}", _esc.SocioB.Id.ToString())
                .Replace("{C}", _esc.SocioC.Id.ToString()).Replace("{X}", Guid.NewGuid().ToString())
                .Replace("{HOY}", GymBro.Application.Services.DiaLocal.Hoy(TimeProvider.System).ToString("yyyy-MM-dd"));
            var cliente = rol == Anonimo ? _esc.Api.CreateClient() : _esc.Api.ClienteComo(_esc.Usuario(rol));
            var pedido = new HttpRequestMessage(new HttpMethod(metodo), url);
            if (metodo is "POST" or "PUT") pedido.Content = new StringContent("{}", Encoding.UTF8, "application/json");

            var r = await cliente.SendAsync(pedido);

            Assert.True((int)r.StatusCode == esperado,
                $"{rol} {metodo} {ruta}: esperado {esperado} ({justificacion}), obtenido {(int)r.StatusCode}");
        }

        /// <summary>API + datos sintéticos compartidos por todas las celdas.</summary>
        public class Escenario : IAsyncLifetime
        {
            public GymBroApiFactory Api { get; } = new();
            public Socio SocioA { get; private set; } = null!;
            public Socio SocioB { get; private set; } = null!;
            public Socio SocioC { get; private set; } = null!;
            private readonly Dictionary<string, Usuario> _usuarios = new();

            public Usuario Usuario(string rol) => _usuarios[rol];

            public async Task InitializeAsync()
            {
                await Api.InitializeAsync();
                var g1 = new Gimnasio { Nombre = "Gimnasio 1 (prueba)", Codigo = "MATRIZ-1" };
                var g2 = new Gimnasio { Nombre = "Gimnasio 2 (prueba)", Codigo = "MATRIZ-2" };
                SocioA = NuevoSocio("Ana", "ana@matriz.test", g1.Id);
                SocioB = NuevoSocio("Bruno", "bruno@matriz.test", g1.Id);
                SocioC = NuevoSocio("Carla", "carla@matriz.test", g2.Id);

                var dueno = new Usuario { Email = "dueno@matriz.test", NombreCompleto = "Dueño Test", Rol = RolUsuario.AdministradorGimnasio, GimnasioId = g1.Id, PasswordHash = "-" };
                var trainer = new Usuario { Email = "trainer@matriz.test", NombreCompleto = "Trainer Test", Rol = RolUsuario.Entrenador, GimnasioId = g1.Id, PasswordHash = "-" };
                var socio = new Usuario { Email = SocioA.Email, NombreCompleto = "Ana Test", Rol = RolUsuario.Socio, SocioId = SocioA.Id, PasswordHash = "-" };
                var superAdmin = new Usuario { Email = "plataforma@matriz.test", NombreCompleto = "Plataforma Test", Rol = RolUsuario.SuperAdministrador, PasswordHash = "-" };
                _usuarios[Dueno] = dueno;
                _usuarios[Trainer] = trainer;
                _usuarios[Socio] = socio;
                _usuarios[SuperAdmin] = superAdmin;

                await Api.ConDbAsync(db =>
                {
                    db.Gimnasios.AddRange(g1, g2);
                    db.Socios.AddRange(SocioA, SocioB, SocioC);
                    db.Usuarios.AddRange(dueno, trainer, socio, superAdmin);
                    db.Asignaciones.Add(new AsignacionEntrenadorSocio { GimnasioId = g1.Id, EntrenadorUsuarioId = trainer.Id, SocioId = SocioA.Id });
                    return Task.CompletedTask;
                });
            }

            public async Task DisposeAsync() => await Api.DisposeAsync();

            private static Socio NuevoSocio(string nombre, string email, Guid gimnasioId) => new()
            {
                Id = Guid.NewGuid(), Nombre = nombre, Apellido = "Test", Email = email, Genero = "F",
                FechaNacimiento = new DateTime(1995, 1, 1, 0, 0, 0, DateTimeKind.Utc), GimnasioId = gimnasioId,
                Objetivo = ObjetivoEntrenamiento.Mantenimiento, Nivel = NivelExperiencia.Principiante,
                ConsentimientoDatosSaludFecha = DateTime.UtcNow,
            };
        }
    }
}
