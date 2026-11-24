using GymBro.Application.DTOs;
using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Domain.Exceptions;
using GymBro.Domain.Repositories;
using GymBro.Domain.Security;

namespace GymBro.Application.Services
{
    public class SocioService : ISocioService
    {
        private readonly ISocioRepository _repository;
        private readonly ISocioAccessService _acceso;
        private readonly IUsuarioActual _actual;
        private readonly IAuditService _audit;
        private readonly IUsuarioRepository _usuarios;
        private readonly IInvitacionRepository _invitaciones;
        private readonly IInvitacionService _invitacionService;
        private readonly IAsignacionRepository _asignaciones;
        private readonly IPrescripcionEntrenamientoRepository _prescripciones;

        public SocioService(ISocioRepository repository, ISocioAccessService acceso, IUsuarioActual actual, IAuditService audit,
            IUsuarioRepository usuarios, IInvitacionRepository invitaciones, IInvitacionService invitacionService,
            IAsignacionRepository asignaciones, IPrescripcionEntrenamientoRepository prescripciones)
        {
            _repository = repository;
            _acceso = acceso;
            _actual = actual;
            _audit = audit;
            _usuarios = usuarios;
            _invitaciones = invitaciones;
            _invitacionService = invitacionService;
            _asignaciones = asignaciones;
            _prescripciones = prescripciones;
        }

        public async Task<IEnumerable<SocioResponseDto>> GetAllSociosAsync()
        {
            // El alcance depende del rol; el aislamiento entre gimnasios lo garantiza el filtro del DbContext.
            IEnumerable<Socio> socios = _actual.Rol switch
            {
                RolUsuario.Entrenador => await _repository.GetByEntrenadorAsync(_actual.UsuarioId ?? throw new AccesoDenegadoException()),
                RolUsuario.Socio => await PropioAsync(),
                RolUsuario.AdministradorGimnasio => await _repository.GetAllAsync(),
                _ => _actual.EsProcesoDeSistema ? await _repository.GetAllAsync() : throw new AccesoDenegadoException()
            };

            var lista = socios.ToList();
            var estados = await EstadosDeAccesoAsync(lista.Select(s => s.Id).ToList());
            return lista.Select(s => Mapear(s, estados.GetValueOrDefault(s.Id, EstadosAcceso.SinAcceso)));
        }

        public async Task<SocioDetalleDto?> GetSocioByIdAsync(Guid id)
        {
            await _acceso.AsegurarAccesoAsync(id);
            var socio = await _repository.GetByIdAsync(id);
            if (socio is null) return null;

            var estado = (await EstadosDeAccesoAsync(new[] { id })).GetValueOrDefault(id, EstadosAcceso.SinAcceso);
            return MapearDetalle(socio, estado);
        }

        public async Task<SocioAltaResponseDto> CreateSocioAsync(SocioCreateDto dto)
        {
            // Alta presencial: solo el Dueño, siempre dentro de su gimnasio.
            var gimnasioId = ExigirDueno();

            var (nombre, apellido) = ValidarNombre(dto.Nombre, dto.Apellido);
            var dni = ValidarDni(dto.Dni);
            var email = AuthService.Normalizar(dto.Email);
            AuthService.ValidarEmail(email);

            if (await _repository.ExisteDniAsync(dni))
                throw new ReglaNegocioException("Ya existe un socio registrado con ese DNI.");
            if (await _usuarios.GetByEmailAsync(email) is not null)
                throw new ReglaNegocioException("Ya existe una cuenta con ese email.");

            var socio = new Socio
            {
                Id = Guid.NewGuid(),
                Nombre = nombre,
                Apellido = apellido,
                Email = email,
                Dni = dni,
                GimnasioId = gimnasioId,
                // Género, fecha de nacimiento y consentimiento los completa el socio al activar su cuenta.
                Objetivo = ObjetivoEntrenamiento.Mantenimiento,
                Nivel = NivelExperiencia.Principiante,
                OrigenDatos = OrigenDatos.Real   // Etapa 7d (P1): alta por el flujo normal de la aplicación
            };

            var nuevoSocio = await _repository.AddAsync(socio);
            await _audit.RegistrarAsync("ALTA_SOCIO", nameof(Socio), nuevoSocio.Id.ToString());
            var invitacion = await _invitacionService.EmitirAsync(nuevoSocio, gimnasioId);

            return new SocioAltaResponseDto
            {
                Socio = Mapear(nuevoSocio, EstadosAcceso.Invitado),
                Invitacion = invitacion
            };
        }

        public async Task<SocioDetalleDto> ActualizarSocioAsync(Guid id, SocioUpdateDto dto)
        {
            var socio = await ObtenerDelGimnasioParaModificarAsync(id);
            await ExigirSinCuentaAsync(id, "modificar");

            var (nombre, apellido) = ValidarNombre(dto.Nombre, dto.Apellido);
            var dni = ValidarDni(dto.Dni);
            var email = AuthService.Normalizar(dto.Email);
            AuthService.ValidarEmail(email);

            if (await _repository.ExisteDniAsync(dni, excluirSocioId: id))
                throw new ReglaNegocioException("Ya existe un socio registrado con ese DNI.");
            if (await _usuarios.GetByEmailAsync(email) is not null)
                throw new ReglaNegocioException("Ya existe una cuenta con ese email.");

            socio.Nombre = nombre;
            socio.Apellido = apellido;
            socio.Email = email;
            socio.Dni = dni;
            socio.FechaModificacion = DateTime.UtcNow;
            await _repository.SaveChangesAsync();
            await _audit.RegistrarAsync("MODIFICACION_SOCIO", nameof(Socio), id.ToString());

            var estado = (await EstadosDeAccesoAsync(new[] { id })).GetValueOrDefault(id, EstadosAcceso.SinAcceso);
            return MapearDetalle(socio, estado);
        }

        public async Task<SocioDetalleDto> CambiarEstadoAsync(Guid id, string estado)
        {
            if (string.IsNullOrWhiteSpace(estado) || int.TryParse(estado, out _)
                || !Enum.TryParse<EstadoSocio>(estado.Trim(), true, out var nuevo) || !Enum.IsDefined(nuevo))
                throw new ReglaNegocioException("El estado debe ser Activo o Inactivo.");

            var socio = await ObtenerDelGimnasioParaModificarAsync(id);
            if (socio.Estado != nuevo)
            {
                socio.Estado = nuevo;
                socio.FechaModificacion = DateTime.UtcNow;
                await _repository.SaveChangesAsync();
                await _audit.RegistrarAsync("CAMBIO_ESTADO_SOCIO", nameof(Socio), id.ToString(), nuevo.ToString());
            }

            var acceso = (await EstadosDeAccesoAsync(new[] { id })).GetValueOrDefault(id, EstadosAcceso.SinAcceso);
            return MapearDetalle(socio, acceso);
        }

        public async Task<InvitacionResponseDto> RenovarInvitacionAsync(Guid id)
        {
            var gimnasioId = ExigirDueno();
            var socio = await ObtenerDelGimnasioParaModificarAsync(id);
            await ExigirSinCuentaAsync(id, "invitar");
            return await _invitacionService.EmitirAsync(socio, gimnasioId);
        }

        public async Task BajaSocioAsync(Guid id)
        {
            var socio = await ObtenerDelGimnasioParaModificarAsync(id);

            // Las rutinas pendientes quedarían sin nadie que las revise cuando el socio deje de ser del gimnasio.
            if ((await _prescripciones.GetPendientesDeRevisionAsync()).Any(p => p.SocioId == id))
                throw new ReglaNegocioException("El socio tiene rutinas pendientes de revisión. Resolvelas antes de darlo de baja.");

            var cuenta = await _usuarios.GetBySocioIdAsync(id);
            await _asignaciones.QuitarDeSocioAsync(id);
            await _invitacionService.RevocarVigentesAsync(id);

            if (cuenta is null)
            {
                // Ficha que nunca se activó: baja lógica. El DNI se libera para poder volver a darlo de alta
                // (el índice único incluye las filas dadas de baja).
                socio.FechaBaja = DateTime.UtcNow;
                socio.Dni = null;
                await _repository.SaveChangesAsync();
                await _audit.RegistrarAsync("BAJA_SOCIO", nameof(Socio), id.ToString());
            }
            else
            {
                // El socio tiene cuenta propia: se desvincula del gimnasio y sigue como socio independiente
                // con su historial, que le pertenece.
                socio.GimnasioId = null;
                socio.FechaModificacion = DateTime.UtcNow;
                await _repository.SaveChangesAsync();
                await _audit.RegistrarAsync("DESVINCULACION_SOCIO", nameof(Socio), id.ToString());
            }
        }

        // ---- Auxiliares ----

        private Guid ExigirDueno()
        {
            var gimnasioId = _actual.GimnasioId ?? throw new AccesoDenegadoException();
            if (_actual.Rol != RolUsuario.AdministradorGimnasio) throw new AccesoDenegadoException();
            return gimnasioId;
        }

        /// <summary>Socio del gimnasio del Dueño, listo para modificar. Lo que no es de su gimnasio responde 404.</summary>
        private async Task<Socio> ObtenerDelGimnasioParaModificarAsync(Guid id)
        {
            var gimnasioId = ExigirDueno();
            var visible = await _repository.GetByIdAsync(id); // pasa por el filtro de tenant
            if (visible is null || visible.GimnasioId != gimnasioId)
                throw new RecursoNoEncontradoException("Socio no encontrado.");
            return await _repository.GetForUpdateAsync(id) ?? throw new RecursoNoEncontradoException("Socio no encontrado.");
        }

        private async Task ExigirSinCuentaAsync(Guid socioId, string accion)
        {
            if (await _usuarios.GetBySocioIdAsync(socioId) is not null)
                throw new ReglaNegocioException(
                    $"El socio ya activó su cuenta: sus datos personales los gestiona él mismo, por eso no se puede {accion} su ficha.");
        }

        private static (string nombre, string apellido) ValidarNombre(string? nombre, string? apellido)
        {
            if (string.IsNullOrWhiteSpace(nombre) || string.IsNullOrWhiteSpace(apellido))
                throw new ReglaNegocioException("Nombre y apellido son obligatorios.");
            return (nombre.Trim(), apellido.Trim());
        }

        private static string ValidarDni(string? crudo)
        {
            var dni = (crudo ?? string.Empty).Replace(".", "").Trim();
            if (dni.Length is < 7 or > 9 || !dni.All(char.IsDigit))
                throw new ReglaNegocioException("El DNI debe tener entre 7 y 9 dígitos.");
            return dni;
        }

        /// <summary>Estado de acceso por socio. Quien consulta su propia ficha tiene cuenta por definición.</summary>
        private async Task<Dictionary<Guid, string>> EstadosDeAccesoAsync(IReadOnlyCollection<Guid> ids)
        {
            var estados = new Dictionary<Guid, string>();
            if (_actual.Rol == RolUsuario.Socio)
            {
                foreach (var id in ids) estados[id] = EstadosAcceso.Activo;
                return estados;
            }

            var conCuenta = await _usuarios.GetSocioIdsConCuentaAsync(ids);
            var invitados = await _invitaciones.GetSocioIdsConInvitacionVigenteAsync(ids);
            foreach (var id in ids)
                estados[id] = conCuenta.Contains(id) ? EstadosAcceso.Activo
                    : invitados.Contains(id) ? EstadosAcceso.Invitado
                    : EstadosAcceso.SinAcceso;
            return estados;
        }

        private async Task<IEnumerable<Socio>> PropioAsync()
        {
            var id = _actual.SocioId ?? throw new AccesoDenegadoException();
            var socio = await _repository.GetByIdAsync(id);
            return socio is null ? Enumerable.Empty<Socio>() : new[] { socio };
        }

        private static SocioResponseDto Mapear(Socio s, string estadoAcceso) => new()
        {
            Id = s.Id,
            NombreCompleto = s.NombreCompleto,
            Email = s.Email,
            FechaAlta = s.FechaAlta,
            GimnasioId = s.GimnasioId,
            EstadoAcceso = estadoAcceso,
            Estado = s.Estado.ToString()
        };

        private SocioDetalleDto MapearDetalle(Socio s, string estadoAcceso)
        {
            // El DNI solo lo ven el Dueño y el propio socio; el Personal Trainer no lo necesita.
            var veDni = _actual.Rol == RolUsuario.AdministradorGimnasio || (_actual.Rol == RolUsuario.Socio && _actual.SocioId == s.Id);
            return new SocioDetalleDto
            {
                Id = s.Id,
                Nombre = s.Nombre,
                Apellido = s.Apellido,
                NombreCompleto = s.NombreCompleto,
                Email = s.Email,
                Dni = veDni ? s.Dni : null,
                Genero = s.Genero,
                FechaNacimiento = s.FechaNacimiento == default ? null : s.FechaNacimiento,
                Objetivo = Enum.IsDefined(s.Objetivo) ? s.Objetivo.ToString() : null,
                Nivel = Enum.IsDefined(s.Nivel) ? s.Nivel.ToString() : null,
                FechaAlta = s.FechaAlta,
                EstadoAcceso = estadoAcceso,
                Estado = s.Estado.ToString()
            };
        }
    }
}
