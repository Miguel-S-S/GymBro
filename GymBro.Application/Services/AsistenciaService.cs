using GymBro.Application.DTOs.AsistenciaCuotas;
using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Domain.Exceptions;
using GymBro.Domain.Repositories;
using GymBro.Domain.Security;

namespace GymBro.Application.Services
{
    public interface IAsistenciaService
    {
        /// <summary>Token de corta duracion para el QR del Socio autenticado.</summary>
        Task<QrAsistenciaDto> GenerarQrAsync();

        /// <summary>El personal valida el token del QR (o el codigo tipeado) y registra la asistencia del dia.</summary>
        Task<RegistroAsistenciaDto> RegistrarPorQrAsync(string? token);

        /// <summary>El personal registra la asistencia de un socio de su gimnasio sin QR.</summary>
        Task<RegistroAsistenciaDto> RegistrarManualAsync(Guid? socioId);

        /// <summary>Asistencias de un dia (por defecto hoy). La plataforma puede omitir el gimnasio (todos).</summary>
        Task<AsistenciaDelDiaDto> ListarDelDiaAsync(DateOnly? fecha, Guid? gimnasioId);
    }

    /// <summary>
    /// Etapa 11: asistencia al gimnasio. Una por socio y dia local (DiaLocal); el registro es idempotente: si ya existe se
    /// devuelve la existente con YaRegistrada = true. Registran el Dueno y el Personal Trainer de su propio gimnasio; la
    /// plataforma solo lee. La asistencia no es un dato de salud.
    /// </summary>
    public class AsistenciaService : IAsistenciaService
    {
        private readonly IAsistenciaRepository _asistencias;
        private readonly ISocioRepository _socios;
        private readonly IGimnasioRepository _gimnasios;
        private readonly IUsuarioActual _actual;
        private readonly TimeProvider _reloj;
        private readonly AsistenciaConfig _config;

        public AsistenciaService(IAsistenciaRepository asistencias, ISocioRepository socios, IGimnasioRepository gimnasios,
            IUsuarioActual actual, TimeProvider reloj, AsistenciaConfig config)
        {
            _asistencias = asistencias;
            _socios = socios;
            _gimnasios = gimnasios;
            _actual = actual;
            _reloj = reloj;
            _config = config;
        }

        public async Task<QrAsistenciaDto> GenerarQrAsync()
        {
            if (_actual.Rol != RolUsuario.Socio || _actual.SocioId is not Guid socioId)
                throw new AccesoDenegadoException("Solo el socio obtiene su código de asistencia.");
            if (!_config.QrHabilitado)
                throw new ReglaNegocioException("El código QR de asistencia no está configurado en este servidor. Pedile al personal que registre tu ingreso a mano.");

            var socio = await _socios.GetByIdAsync(socioId) ?? throw new RecursoNoEncontradoException("Socio no encontrado.");
            if (socio.GimnasioId is null)
                throw new ReglaNegocioException("Todavía no estás vinculado a un gimnasio.");

            var vence = _reloj.GetUtcNow().AddSeconds(_config.DuracionTokenSegundos);
            return new QrAsistenciaDto
            {
                Token = TokenAsistencia.Generar(socioId, vence, _config.ClaveQr),
                ExpiraUtc = vence.UtcDateTime,
                DuracionSegundos = _config.DuracionTokenSegundos,
            };
        }

        public async Task<RegistroAsistenciaDto> RegistrarPorQrAsync(string? token)
        {
            var gimnasioId = GimnasioDelPersonal();
            if (!_config.QrHabilitado)
                throw new ReglaNegocioException("El código QR de asistencia no está configurado en este servidor. Registrá el ingreso a mano.");

            var resultado = TokenAsistencia.Validar(token, _reloj.GetUtcNow(), _config.ClaveQr, out var socioId);
            switch (resultado)
            {
                case TokenAsistencia.Resultado.Vencido:
                    throw new ReglaNegocioException("El código venció. Pedile al socio que lo actualice en su aplicación.");
                case TokenAsistencia.Resultado.Malformado:
                case TokenAsistencia.Resultado.FirmaInvalida:
                    throw new ReglaNegocioException("El código no es válido.");
            }

            return await RegistrarAsync(socioId, gimnasioId, MetodoAsistencia.Qr);
        }

        public async Task<RegistroAsistenciaDto> RegistrarManualAsync(Guid? socioId)
        {
            var gimnasioId = GimnasioDelPersonal();
            if (socioId is not Guid id || id == Guid.Empty) throw new ReglaNegocioException("Indique el socio.");
            return await RegistrarAsync(id, gimnasioId, MetodoAsistencia.Manual);
        }

        public async Task<AsistenciaDelDiaDto> ListarDelDiaAsync(DateOnly? fecha, Guid? gimnasioId)
        {
            var alcance = await AlcanceLecturaAsync(_actual, _gimnasios, gimnasioId);
            var dia = fecha ?? DiaLocal.Hoy(_reloj);
            var filas = await _asistencias.ListarAsync(dia, alcance);
            return new AsistenciaDelDiaDto { Fecha = dia, Total = filas.Count, Asistencias = filas.Select(Mapear).ToList() };
        }

        /// <summary>
        /// Gimnasio que se lee. Plataforma: el indicado (validado) o null = todos. Dueno y Personal Trainer: el propio; otro
        /// gimnasio responde 404 (no se revela). El Socio no lee listados de gestion (403).
        /// </summary>
        internal static async Task<Guid?> AlcanceLecturaAsync(IUsuarioActual actual, IGimnasioRepository gimnasios, Guid? indicado)
        {
            if (actual.Rol == RolUsuario.SuperAdministrador)
            {
                if (indicado is Guid g)
                    _ = await gimnasios.GetByIdAsync(g) ?? throw new RecursoNoEncontradoException("Gimnasio no encontrado.");
                return indicado;
            }
            if (actual.Rol is RolUsuario.AdministradorGimnasio or RolUsuario.Entrenador)
                return await GimnasioDestino.ResolverAsync(actual, gimnasios, indicado);
            throw new AccesoDenegadoException();
        }

        private Guid GimnasioDelPersonal()
        {
            if (_actual.Rol is not (RolUsuario.AdministradorGimnasio or RolUsuario.Entrenador) || _actual.GimnasioId is not Guid g)
                throw new AccesoDenegadoException("Solo el personal del gimnasio registra asistencias.");
            return g;
        }

        private async Task<RegistroAsistenciaDto> RegistrarAsync(Guid socioId, Guid gimnasioId, MetodoAsistencia metodo)
        {
            // El filtro entre gimnasios del DbContext solo devuelve socios del gimnasio de quien registra: un socio de otro
            // gimnasio (o un token de otro gimnasio) responde 404 sin revelar que existe.
            var socio = await _socios.GetByIdAsync(socioId);
            if (socio is null || socio.GimnasioId != gimnasioId)
                throw new RecursoNoEncontradoException("Socio no encontrado en este gimnasio.");

            var ahora = _reloj.GetUtcNow();
            var hoy = DiaLocal.DeInstante(ahora);
            var existente = await _asistencias.GetDelDiaAsync(socioId, hoy);
            if (existente is not null)
                return await ResultadoAsync(existente, socio, yaRegistrada: true);

            var nueva = new Asistencia
            {
                SocioId = socioId,
                GimnasioId = gimnasioId,
                Instante = ahora.UtcDateTime,
                Fecha = hoy,
                Metodo = metodo,
                RegistradoPorUsuarioId = _actual.UsuarioId ?? throw new AccesoDenegadoException(),
                FechaAlta = ahora.UtcDateTime,
                // Etapa 7e: la asistencia hereda el origen del socio (como la rutina en el planificador).
                OrigenDatos = socio.OrigenDatos,
            };
            _asistencias.Agregar(nueva);
            if (!await _asistencias.GuardarCambiosAsync())
            {
                // Dos registros simultaneos del mismo socio y dia: el indice unico deja uno solo y se devuelve ese.
                _asistencias.Descartar(nueva);
                var ganadora = await _asistencias.GetDelDiaAsync(socioId, hoy)
                    ?? throw new ReglaNegocioException("No se pudo registrar la asistencia. Intente de nuevo.");
                return await ResultadoAsync(ganadora, socio, yaRegistrada: true);
            }
            return await ResultadoAsync(nueva, socio, yaRegistrada: false);
        }

        private async Task<RegistroAsistenciaDto> ResultadoAsync(Asistencia a, Socio socio, bool yaRegistrada)
        {
            var gimnasio = await _gimnasios.GetByIdAsync(a.GimnasioId);
            return new RegistroAsistenciaDto
            {
                YaRegistrada = yaRegistrada,
                Asistencia = Mapear(new FilaAsistencia(a, socio.NombreCompleto, gimnasio?.Nombre ?? string.Empty)),
            };
        }

        private static AsistenciaDto Mapear(FilaAsistencia f) => new()
        {
            Id = f.Asistencia.Id,
            SocioId = f.Asistencia.SocioId,
            SocioNombre = f.SocioNombre,
            GimnasioId = f.Asistencia.GimnasioId,
            GimnasioNombre = f.GimnasioNombre,
            Instante = f.Asistencia.Instante,
            Fecha = f.Asistencia.Fecha,
            Metodo = f.Asistencia.Metodo.ToString(),
            RegistradoPorUsuarioId = f.Asistencia.RegistradoPorUsuarioId,
        };
    }
}
