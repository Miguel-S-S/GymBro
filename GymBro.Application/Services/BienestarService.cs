using GymBro.Application.Abstractions;
using GymBro.Application.DTOs.Bienestar;
using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Domain.Exceptions;
using GymBro.Domain.Repositories;
using GymBro.Domain.Security;

namespace GymBro.Application.Services
{
    public interface IBienestarService
    {
        /// <summary>Día local del servidor y el registro del socio autenticado para ese día (null si no hay).</summary>
        Task<BienestarHoyDto> ObtenerHoyAsync();

        /// <summary>Alta o actualización (idempotente) del registro del socio autenticado para hoy o ayer.</summary>
        Task<RegistroBienestarDto> GuardarAsync(DateOnly fecha, RegistroBienestarRequest request);

        /// <summary>Historial de un socio, según el alcance del rol (ISocioAccessService).</summary>
        Task<IReadOnlyList<RegistroBienestarDto>> ListarAsync(Guid socioId, DateOnly? desde, DateOnly? hasta);
    }

    /// <summary>
    /// Registro diario de bienestar (Etapa 8b). El día se calcula en el servidor con el reloj inyectado
    /// (TimeProvider) en la zona de DiaLocal; nunca se toma del cliente.
    /// </summary>
    public class BienestarService : IBienestarService
    {
        /// <summary>Historial por defecto cuando no se indican fechas: los últimos 30 días.</summary>
        public const int DiasHistorialPorDefecto = 30;
        /// <summary>Máximo rango consultable en un pedido.</summary>
        public const int DiasHistorialMaximo = 366;

        private readonly IRegistroBienestarRepository _registros;
        private readonly ISocioRepository _socios;
        private readonly ISocioAccessService _acceso;
        private readonly IUsuarioActual _actual;
        private readonly TimeProvider _reloj;
        private readonly IReglasSeguridadService _reglas;
        private readonly IRegistroEventos _eventos;

        public BienestarService(IRegistroBienestarRepository registros, ISocioRepository socios,
            ISocioAccessService acceso, IUsuarioActual actual, TimeProvider reloj, IReglasSeguridadService reglas,
            IRegistroEventos eventos)
        {
            _reglas = reglas;
            _eventos = eventos;
            _registros = registros;
            _socios = socios;
            _acceso = acceso;
            _actual = actual;
            _reloj = reloj;
        }

        public async Task<BienestarHoyDto> ObtenerHoyAsync()
        {
            var socio = await SocioAutenticadoAsync();
            var hoy = DiaLocal.Hoy(_reloj);
            // Sin consentimiento no hay registros que mostrar (Decisión C).
            var registro = socio.ConsentimientoDatosSaludFecha is null ? null : await _registros.GetDelDiaAsync(socio.Id, hoy);
            return new BienestarHoyDto { FechaLocal = hoy, Registro = registro is null ? null : Mapear(registro) };
        }

        public async Task<RegistroBienestarDto> GuardarAsync(DateOnly fecha, RegistroBienestarRequest request)
        {
            var socio = await SocioAutenticadoAsync();

            var hoy = DiaLocal.Hoy(_reloj);
            if (fecha != hoy && fecha != hoy.AddDays(-1))
                throw new ReglaNegocioException($"Solo se puede cargar o corregir el registro de hoy ({hoy:yyyy-MM-dd}) o de ayer.");

            if (socio.ConsentimientoDatosSaludFecha is null)
                throw new ReglaNegocioException(MensajeSinConsentimiento);

            var valores = Validar(request);

            var registro = await _registros.GetDelDiaAsync(socio.Id, fecha);
            var fcAnterior = registro?.FrecuenciaCardiacaReposo;
            if (registro is null)
            {
                registro = new RegistroBienestarDiario { SocioId = socio.Id, Fecha = fecha, FechaAlta = _reloj.GetUtcNow().UtcDateTime };
                _registros.Agregar(registro);
            }
            else
            {
                registro.FechaModificacion = _reloj.GetUtcNow().UtcDateTime;
            }

            Aplicar(valores, registro);
            await _registros.GuardarCambiosAsync();

            // Etapa 10a (R-FC-1): cuando hay FC de reposo, o cuando se corrige o se borra. Tolerante a fallos (D6).
            if (fcAnterior is not null || registro.FrecuenciaCardiacaReposo is not null)
            {
                try
                {
                    await _reglas.EvaluarBienestarAsync(registro.Id);
                }
                catch (Exception ex)
                {
                    _eventos.Error($"Fallo la evaluacion de reglas de seguridad del registro de bienestar {registro.Id}.", ex);
                }
            }
            return Mapear(registro);
        }

        public async Task<IReadOnlyList<RegistroBienestarDto>> ListarAsync(Guid socioId, DateOnly? desde, DateOnly? hasta)
        {
            // 404 si el socio no es visible para quien llama; 403 si el rol no puede actuar (T7).
            await _acceso.AsegurarAccesoAsync(socioId);
            var socio = await _socios.GetByIdAsync(socioId) ?? throw new RecursoNoEncontradoException("Socio no encontrado.");

            var fin = hasta ?? DiaLocal.Hoy(_reloj);
            var inicio = desde ?? fin.AddDays(-(DiasHistorialPorDefecto - 1));
            if (inicio > fin)
                throw new ReglaNegocioException("La fecha 'desde' no puede ser posterior a 'hasta'.");
            if (fin.DayNumber - inicio.DayNumber + 1 > DiasHistorialMaximo)
                throw new ReglaNegocioException($"El rango consultado no puede superar {DiasHistorialMaximo} días.");

            if (socio.ConsentimientoDatosSaludFecha is null)
            {
                // Decisión C: el propio socio ve una lista vacía; el personal del gimnasio recibe 400.
                if (_actual.Rol == RolUsuario.Socio) return Array.Empty<RegistroBienestarDto>();
                throw new ReglaNegocioException(MensajeSinConsentimiento);
            }

            var registros = await _registros.ListarAsync(socioId, inicio, fin);
            return registros.Select(Mapear).ToList();
        }

        private const string MensajeSinConsentimiento =
            "El socio todavía no otorgó el consentimiento para el tratamiento de sus datos de salud.";

        private async Task<Socio> SocioAutenticadoAsync()
        {
            if (_actual.Rol != RolUsuario.Socio || _actual.SocioId is not Guid socioId)
                throw new AccesoDenegadoException("Solo el socio puede cargar su registro de bienestar.");
            return await _socios.GetByIdAsync(socioId) ?? throw new RecursoNoEncontradoException("Socio no encontrado.");
        }

        /// <summary>Valores ya validados y normalizados del pedido.</summary>
        public sealed record ValoresBienestar(
            decimal? HorasSueno, short? Estres, short? Fatiga, decimal? PesoKg, NutricionCategoria? NutricionCategoria,
            decimal? IngestaAguaLitros, string? NutricionNotas, short? FrecuenciaCardiacaReposo);

        /// <summary>Rangos de RangosBienestar y "al menos un dato". Lanza ReglaNegocioException (400) con el motivo.</summary>
        public static ValoresBienestar Validar(RegistroBienestarRequest r)
        {
            ArgumentNullException.ThrowIfNull(r);

            if (r.HorasSueno is decimal sueno &&
                (sueno < RangosBienestar.HorasSuenoMin || sueno > RangosBienestar.HorasSuenoMax || sueno % RangosBienestar.HorasSuenoPaso != 0))
                throw new ReglaNegocioException("Las horas de sueño van de 0 a 24, de a media hora.");

            ValidarEscala(r.Estres, "estrés");
            ValidarEscala(r.Fatiga, "fatiga");

            if (r.PesoKg is decimal peso &&
                (peso < RangosBienestar.PesoMin || peso > RangosBienestar.PesoMax || decimal.Round(peso, 1) != peso))
                throw new ReglaNegocioException($"El peso va de {RangosBienestar.PesoMin} a {RangosBienestar.PesoMax} kg, con un decimal como máximo.");

            if (r.IngestaAguaLitros is decimal agua &&
                (agua < RangosBienestar.AguaMin || agua > RangosBienestar.AguaMax || decimal.Round(agua, 1) != agua))
                throw new ReglaNegocioException($"El agua va de {RangosBienestar.AguaMin} a {RangosBienestar.AguaMax} litros, con un decimal como máximo.");

            if (r.FrecuenciaCardiacaReposo is short fc && (fc < RangosBienestar.FcReposoMin || fc > RangosBienestar.FcReposoMax))
                throw new ReglaNegocioException(
                    $"La frecuencia cardíaca en reposo va de {RangosBienestar.FcReposoMin} a {RangosBienestar.FcReposoMax} latidos por minuto.");

            NutricionCategoria? categoria = null;
            if (!string.IsNullOrWhiteSpace(r.NutricionCategoria))
            {
                var texto = r.NutricionCategoria.Trim();
                if (!Enum.GetNames<NutricionCategoria>().Contains(texto, StringComparer.OrdinalIgnoreCase))
                    throw new ReglaNegocioException("La ingesta del día es Baja, Habitual o Alta.");
                categoria = Enum.Parse<NutricionCategoria>(texto, ignoreCase: true);
            }

            var notas = string.IsNullOrWhiteSpace(r.NutricionNotas) ? null : r.NutricionNotas.Trim();
            if (notas is { Length: > RangosBienestar.NotasMaxLargo })
                throw new ReglaNegocioException($"La nota admite hasta {RangosBienestar.NotasMaxLargo} caracteres.");

            var valores = new ValoresBienestar(r.HorasSueno, r.Estres, r.Fatiga, r.PesoKg, categoria,
                r.IngestaAguaLitros, notas, r.FrecuenciaCardiacaReposo);

            var prueba = new RegistroBienestarDiario();
            Aplicar(valores, prueba);
            if (!prueba.TieneAlgunDato())
                throw new ReglaNegocioException("Cargá al menos un dato (sueño, estrés, fatiga, peso, ingesta, agua, frecuencia cardíaca o nota).");

            return valores;
        }

        private static void ValidarEscala(short? valor, string nombre)
        {
            if (valor is short v && (v < RangosBienestar.EscalaMin || v > RangosBienestar.EscalaMax))
                throw new ReglaNegocioException($"El nivel de {nombre} va de {RangosBienestar.EscalaMin} (mejor) a {RangosBienestar.EscalaMax} (peor).");
        }

        /// <summary>El PUT reemplaza el registro del día completo: un dato que no se envía queda como "sin dato".</summary>
        private static void Aplicar(ValoresBienestar v, RegistroBienestarDiario r)
        {
            r.HorasSueno = v.HorasSueno;
            r.Estres = v.Estres;
            r.Fatiga = v.Fatiga;
            r.PesoKg = v.PesoKg;
            r.NutricionCategoria = v.NutricionCategoria;
            r.IngestaAguaLitros = v.IngestaAguaLitros;
            r.NutricionNotas = v.NutricionNotas;
            r.FrecuenciaCardiacaReposo = v.FrecuenciaCardiacaReposo;
        }

        private static RegistroBienestarDto Mapear(RegistroBienestarDiario r) => new()
        {
            Id = r.Id,
            SocioId = r.SocioId,
            Fecha = r.Fecha,
            HorasSueno = r.HorasSueno,
            Estres = r.Estres,
            Fatiga = r.Fatiga,
            PesoKg = r.PesoKg,
            NutricionCategoria = r.NutricionCategoria?.ToString(),
            IngestaAguaLitros = r.IngestaAguaLitros,
            NutricionNotas = r.NutricionNotas,
            FrecuenciaCardiacaReposo = r.FrecuenciaCardiacaReposo,
            FechaAlta = r.FechaAlta,
            FechaModificacion = r.FechaModificacion,
        };
    }
}
