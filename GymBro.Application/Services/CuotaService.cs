using GymBro.Application.DTOs.AsistenciaCuotas;
using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Domain.Exceptions;
using GymBro.Domain.Repositories;
using GymBro.Domain.Security;

namespace GymBro.Application.Services
{
    public interface ICuotaService
    {
        /// <summary>El Dueño genera las cuotas del período para los socios activos de su gimnasio (idempotente).</summary>
        Task<GenerarCuotasResultadoDto> GenerarMesAsync(GenerarCuotasRequest request);

        /// <summary>El Dueño registra a mano el pago de una cuota pendiente.</summary>
        Task<CuotaDto> RegistrarPagoAsync(Guid cuotaId, RegistrarPagoRequest? request);

        /// <summary>Cuotas de un período. La plataforma puede omitir el gimnasio (todos).</summary>
        Task<IReadOnlyList<CuotaDto>> ListarPeriodoAsync(int anio, int mes, Guid? gimnasioId);

        /// <summary>Cuotas vencidas (morosidad derivada). La plataforma puede omitir el gimnasio (todos).</summary>
        Task<IReadOnlyList<CuotaDto>> ListarMorososAsync(Guid? gimnasioId);

        /// <summary>Cuotas del Socio autenticado.</summary>
        Task<IReadOnlyList<CuotaDto>> MisCuotasAsync();
    }

    /// <summary>
    /// Etapa 11: cuotas mensuales sin pasarela de pago. Gestiona el Dueño de su gimnasio; la plataforma solo lee entre
    /// gimnasios; el Socio ve las suyas; el Personal Trainer no accede. "Vencida" se deriva al consultar.
    /// </summary>
    public class CuotaService : ICuotaService
    {
        private readonly ICuotaRepository _cuotas;
        private readonly ISocioRepository _socios;
        private readonly IGimnasioRepository _gimnasios;
        private readonly IUsuarioActual _actual;
        private readonly TimeProvider _reloj;
        private readonly IAuditService _auditoria;

        public CuotaService(ICuotaRepository cuotas, ISocioRepository socios, IGimnasioRepository gimnasios,
            IUsuarioActual actual, TimeProvider reloj, IAuditService auditoria)
        {
            _cuotas = cuotas;
            _socios = socios;
            _gimnasios = gimnasios;
            _actual = actual;
            _reloj = reloj;
            _auditoria = auditoria;
        }

        public async Task<GenerarCuotasResultadoDto> GenerarMesAsync(GenerarCuotasRequest request)
        {
            var gimnasioId = GimnasioDelDueno();
            ArgumentNullException.ThrowIfNull(request);
            ValidarPeriodo(request.Anio, request.Mes);
            if (request.Monto < RangosCuota.MontoMin || request.Monto > RangosCuota.MontoMax || decimal.Round(request.Monto, 2) != request.Monto)
                throw new ReglaNegocioException("El monto debe ser mayor que cero, con dos decimales como máximo.");
            var vencimiento = request.Vencimiento ?? throw new ReglaNegocioException("Indique el vencimiento.");

            // El filtro entre gimnasios ya limita la lista a los socios del gimnasio del Dueño.
            var activos = (await _socios.GetAllAsync())
                .Where(s => s.GimnasioId == gimnasioId && s.Estado == EstadoSocio.Activo)
                .ToDictionary(s => s.Id, s => s.OrigenDatos);
            var conCuota = await _cuotas.SociosConCuotaAsync(gimnasioId, request.Anio, request.Mes);
            var ahora = _reloj.GetUtcNow().UtcDateTime;
            var nuevas = activos.Keys.Where(id => !conCuota.Contains(id)).OrderBy(id => id).Select(id => new CuotaSocio
            {
                SocioId = id,
                // Etapa 7e: la cuota hereda el origen del socio (como la rutina en el planificador).
                OrigenDatos = activos[id],
                GimnasioId = gimnasioId,
                Anio = request.Anio,
                Mes = request.Mes,
                Monto = request.Monto,
                Vencimiento = vencimiento,
                Estado = EstadoCuota.Pendiente,
                FechaAlta = ahora,
            }).ToList();

            if (nuevas.Count > 0)
            {
                _cuotas.AgregarRango(nuevas);
                if (!await _cuotas.GuardarCambiosAsync())
                    throw new ReglaNegocioException("Las cuotas de ese período se están generando desde otro pedido. Volvé a consultar la lista.");
                await _auditoria.RegistrarAsync("GenerarCuotas", nameof(CuotaSocio), $"{request.Anio:D4}-{request.Mes:D2}",
                    $"Creadas {nuevas.Count}; existentes {conCuota.Count}; monto {request.Monto}; vencimiento {vencimiento:yyyy-MM-dd}");
            }
            return new GenerarCuotasResultadoDto { Creadas = nuevas.Count, Existentes = activos.Count - nuevas.Count };
        }

        public async Task<CuotaDto> RegistrarPagoAsync(Guid cuotaId, RegistrarPagoRequest? request)
        {
            var gimnasioId = GimnasioDelDueno();
            var cuota = await _cuotas.GetForUpdateAsync(cuotaId);
            if (cuota is null || cuota.GimnasioId != gimnasioId) throw new RecursoNoEncontradoException("Cuota no encontrada.");
            if (cuota.Estado == EstadoCuota.Pagada) throw new ReglaNegocioException("La cuota ya figura como pagada.");

            var hoy = DiaLocal.Hoy(_reloj);
            var fecha = request?.FechaPago ?? hoy;
            if (fecha > hoy) throw new ReglaNegocioException("La fecha de pago no puede ser posterior a hoy.");

            cuota.Estado = EstadoCuota.Pagada;
            cuota.FechaPago = fecha;
            cuota.FechaModificacion = _reloj.GetUtcNow().UtcDateTime;
            await _cuotas.GuardarCambiosAsync();
            await _auditoria.RegistrarAsync("RegistrarPagoCuota", nameof(CuotaSocio), cuota.Id.ToString(),
                $"Período {cuota.Anio:D4}-{cuota.Mes:D2}; fecha de pago {fecha:yyyy-MM-dd}");

            var socio = await _socios.GetByIdAsync(cuota.SocioId);
            var gimnasio = await _gimnasios.GetByIdAsync(cuota.GimnasioId);
            return Mapear(new FilaCuota(cuota, socio?.NombreCompleto ?? string.Empty, gimnasio?.Nombre ?? string.Empty), hoy);
        }

        public async Task<IReadOnlyList<CuotaDto>> ListarPeriodoAsync(int anio, int mes, Guid? gimnasioId)
        {
            ValidarPeriodo(anio, mes);
            var alcance = await AlcanceGestionAsync(gimnasioId);
            var hoy = DiaLocal.Hoy(_reloj);
            return (await _cuotas.ListarPeriodoAsync(anio, mes, alcance)).Select(f => Mapear(f, hoy)).ToList();
        }

        public async Task<IReadOnlyList<CuotaDto>> ListarMorososAsync(Guid? gimnasioId)
        {
            var alcance = await AlcanceGestionAsync(gimnasioId);
            var hoy = DiaLocal.Hoy(_reloj);
            return (await _cuotas.ListarVencidasAsync(hoy, alcance)).Select(f => Mapear(f, hoy)).ToList();
        }

        public async Task<IReadOnlyList<CuotaDto>> MisCuotasAsync()
        {
            if (_actual.Rol != RolUsuario.Socio || _actual.SocioId is not Guid socioId)
                throw new AccesoDenegadoException("Solo el socio consulta sus cuotas.");
            var hoy = DiaLocal.Hoy(_reloj);
            var cuotas = await _cuotas.ListarDelSocioAsync(socioId);
            return cuotas.Select(c => Mapear(new FilaCuota(c, string.Empty, string.Empty), hoy)).ToList();
        }

        /// <summary>Lectura de gestión: Dueño (su gimnasio) y plataforma (todos o el indicado). El Personal Trainer no.</summary>
        private Task<Guid?> AlcanceGestionAsync(Guid? indicado)
        {
            if (_actual.Rol is not (RolUsuario.SuperAdministrador or RolUsuario.AdministradorGimnasio))
                throw new AccesoDenegadoException();
            return AsistenciaService.AlcanceLecturaAsync(_actual, _gimnasios, indicado);
        }

        private Guid GimnasioDelDueno()
        {
            if (_actual.Rol != RolUsuario.AdministradorGimnasio || _actual.GimnasioId is not Guid g)
                throw new AccesoDenegadoException("Solo el Dueño del gimnasio gestiona las cuotas.");
            return g;
        }

        private static void ValidarPeriodo(int anio, int mes)
        {
            if (anio < RangosCuota.AnioMin || anio > RangosCuota.AnioMax || mes < 1 || mes > 12)
                throw new ReglaNegocioException("El período no es válido (año de 2000 a 2100 y mes de 1 a 12).");
        }

        internal static CuotaDto Mapear(FilaCuota f, DateOnly hoy)
        {
            var c = f.Cuota;
            var vencida = c.EstaVencida(hoy);
            return new CuotaDto
            {
                Id = c.Id,
                SocioId = c.SocioId,
                SocioNombre = f.SocioNombre,
                GimnasioId = c.GimnasioId,
                GimnasioNombre = f.GimnasioNombre,
                Anio = c.Anio,
                Mes = c.Mes,
                Monto = c.Monto,
                Vencimiento = c.Vencimiento,
                FechaPago = c.FechaPago,
                Estado = c.Estado.ToString(),
                Vencida = vencida,
                DiasVencida = vencida ? hoy.DayNumber - c.Vencimiento.DayNumber : 0,
            };
        }
    }
}
