using GymBro.Application.DTOs.Acceso;
using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Domain.Exceptions;
using GymBro.Domain.Repositories;
using GymBro.Domain.Security;
using System.Text.Json;
using GymBro.Application.DTOs;
using GymBro.Domain.Rules;

namespace GymBro.Application.Services
{
    public interface IRevisionPrescripcionService
    {
        /// <summary>
        /// Define el estado inicial de una rutina recién generada por la IA según el contexto del socio:
        /// independiente o gimnasio sin política de revisión -> Aprobada; en otro caso -> PendienteRevision.
        /// Lo invoca el generador de prescripciones antes de persistir.
        /// </summary>
        Task AplicarPoliticaAsync(PrescripcionEntrenamiento prescripcion);
        Task<IEnumerable<RutinaPendienteDto>> ListarPendientesAsync();
        Task<RutinaPendienteDto> RevisarAsync(Guid prescripcionId, RevisarRutinaRequest request);
    }

    public class RevisionPrescripcionService : IRevisionPrescripcionService
    {
        private readonly IPrescripcionEntrenamientoRepository _prescripciones;
        private readonly ISocioRepository _socios;
        private readonly IGimnasioRepository _gimnasios;
        private readonly IAsignacionRepository _asignaciones;
        private readonly ISocioAccessService _acceso;
        private readonly IUsuarioActual _actual;
        private readonly IAuditService _audit;
        private readonly IValidadorPrescripcionService _validador;

        public RevisionPrescripcionService(IPrescripcionEntrenamientoRepository prescripciones, ISocioRepository socios,
            IGimnasioRepository gimnasios, IAsignacionRepository asignaciones, ISocioAccessService acceso,
            IUsuarioActual actual, IAuditService audit, IValidadorPrescripcionService validador)
        {
            _validador = validador;
            _prescripciones = prescripciones;
            _socios = socios;
            _gimnasios = gimnasios;
            _asignaciones = asignaciones;
            _acceso = acceso;
            _actual = actual;
            _audit = audit;
        }

        public async Task AplicarPoliticaAsync(PrescripcionEntrenamiento prescripcion)
        {
            var socio = await _socios.GetForUpdateAsync(prescripcion.SocioId)
                ?? throw new RecursoNoEncontradoException("Socio no encontrado.");

            var requiere = socio.GimnasioId is Guid gid && await _gimnasios.RequiereAprobacionAsync(gid);
            prescripcion.DefinirEstadoInicial(requiere);
        }

        public async Task<IEnumerable<RutinaPendienteDto>> ListarPendientesAsync()
        {
            var pendientes = (await _prescripciones.GetPendientesDeRevisionAsync()).ToList();

            if (_actual.Rol == RolUsuario.Entrenador)
            {
                var uid = _actual.UsuarioId ?? throw new AccesoDenegadoException();
                var asignados = (await _asignaciones.GetSocioIdsAsync(uid)).ToHashSet();
                pendientes = pendientes.Where(p => asignados.Contains(p.SocioId)).ToList();
            }

            return pendientes.Select(Mapear);
        }

        private static readonly EstadoPrescripcion[] DecisionesDeRevision =
            { EstadoPrescripcion.Aprobada, EstadoPrescripcion.Editada, EstadoPrescripcion.Rechazada };

        /// <summary>
        /// Solo admite los nombres Aprobada, Editada o Rechazada (sin distinguir mayúsculas).
        /// No usa Enum.TryParse: aceptaba números ("5" = Aprobada) y listas separadas por coma ("Aprobada,Editada").
        /// </summary>
        public static EstadoPrescripcion LeerDecision(string? texto)
        {
            var valor = (texto ?? string.Empty).Trim();
            foreach (var d in DecisionesDeRevision)
                if (string.Equals(d.ToString(), valor, StringComparison.OrdinalIgnoreCase))
                    return d;
            throw new ReglaNegocioException("Decisión inválida. Use Aprobada, Editada o Rechazada.");
        }

        public async Task<RutinaPendienteDto> RevisarAsync(Guid prescripcionId, RevisarRutinaRequest request)
        {
            var revisorId = _actual.UsuarioId ?? throw new AccesoDenegadoException();

            var decision = LeerDecision(request.Decision);

            var rutina = await _prescripciones.GetForRevisionAsync(prescripcionId)
                ?? throw new RecursoNoEncontradoException("Rutina no encontrada.");

            // El trainer solo puede revisar rutinas de sus socios asignados.
            await _acceso.AsegurarAccesoAsync(rutina.SocioId);

            if (decision == EstadoPrescripcion.Editada)
            {
                if (request.Ajustes.Count == 0)
                    throw new ReglaNegocioException("Para marcar la rutina como editada indique al menos un ajuste.");
                AplicarAjustes(rutina, request.Ajustes);
            }
            else if (request.Ajustes.Count > 0)
            {
                throw new ReglaNegocioException("Los ajustes solo se aplican con la decisión 'Editada'.");
            }

            var revision = rutina.Revisar(decision, revisorId, request.Comentario);

            // Etapa 10a (R-LIM, decisión D3): al aprobar o editar, las violaciones de límites quedan registradas en la
            // revisión y se muestran al revisor; no bloquean (los valores imposibles ya se rechazaron en AplicarAjustes).
            if (decision != EstadoPrescripcion.Rechazada)
            {
                var violaciones = _validador.Validar(rutina, rutina.Socio?.Nivel);
                if (violaciones.Count > 0)
                    revision.ViolacionesLimites = JsonSerializer.Serialize(violaciones.Select(v => new { codigo = v.Codigo, mensaje = v.Mensaje, orden = v.Orden }));
            }
            await _prescripciones.AddRevisionAsync(revision);
            await _prescripciones.SaveChangesAsync();
            await _audit.RegistrarAsync("REVISION_RUTINA", nameof(PrescripcionEntrenamiento), rutina.Id.ToString(), decision.ToString());
            return Mapear(rutina);
        }

        private static void AplicarAjustes(PrescripcionEntrenamiento rutina, IEnumerable<AjusteEjercicioDto> ajustes)
        {
            foreach (var a in ajustes)
            {
                var pe = rutina.EjerciciosPrescritos.FirstOrDefault(e => e.Id == a.PrescripcionEjercicioId)
                    ?? throw new ReglaNegocioException("Un ejercicio del ajuste no pertenece a la rutina.");

                var series = a.SeriesObjetivo ?? pe.SeriesObjetivo;
                var repMin = a.RepeticionesMinimas ?? pe.RepeticionesMinimas;
                var repMax = a.RepeticionesMaximas ?? pe.RepeticionesMaximas;
                var rir = a.RIRObjetivo ?? pe.RIRObjetivo;
                var descanso = a.DescansoObjetivoSegundos ?? pe.DescansoObjetivoSegundos;

                // Mismas reglas que los CHECK de la base de datos.
                if (series <= 0) throw new ReglaNegocioException("Las series deben ser mayores a 0.");
                if (repMin <= 0 || repMax < repMin) throw new ReglaNegocioException("Rango de repeticiones inválido.");
                if (rir is < 0 or > 5) throw new ReglaNegocioException("El RIR debe estar entre 0 y 5.");
                if (descanso <= 0) throw new ReglaNegocioException("El descanso debe ser mayor a 0.");

                // Etapa 7d (6.4): la rutina queda marcada como modificada solo si un ajuste cambia realmente la dosis.
                if (series != pe.SeriesObjetivo || repMin != pe.RepeticionesMinimas || repMax != pe.RepeticionesMaximas
                    || rir != pe.RIRObjetivo || descanso != pe.DescansoObjetivoSegundos)
                    rutina.EsModificada = true;

                pe.SeriesObjetivo = series;
                pe.RepeticionesMinimas = repMin;
                pe.RepeticionesMaximas = repMax;
                pe.RIRObjetivo = rir;
                pe.DescansoObjetivoSegundos = descanso;
                if (a.Observaciones is not null) pe.Observaciones = a.Observaciones;
                pe.FechaModificacion = DateTime.UtcNow;
            }
        }

        private RutinaPendienteDto Mapear(PrescripcionEntrenamiento p) => new()
        {
            Id = p.Id,
            SocioId = p.SocioId,
            NombreSocio = p.Socio?.NombreCompleto ?? string.Empty,
            OrigenGeneracion = p.OrigenGeneracion,
            Observaciones = p.Observaciones,
            Estado = p.Estado.ToString(),
            FechaAlta = p.FechaAlta,
            Ejercicios = p.EjerciciosPrescritos.OrderBy(e => e.Orden).Select(e => new RutinaEjercicioDto
            {
                Id = e.Id,
                EjercicioId = e.EjercicioId,
                Orden = e.Orden,
                Ejercicio = e.Ejercicio?.Nombre ?? string.Empty,
                SeriesObjetivo = e.SeriesObjetivo,
                RepeticionesMinimas = e.RepeticionesMinimas,
                RepeticionesMaximas = e.RepeticionesMaximas,
                TipoMedida = (e.Ejercicio?.TipoMedida ?? TipoMedida.Repeticiones).ToString(),
                RIRObjetivo = e.RIRObjetivo,
                DescansoObjetivoSegundos = e.DescansoObjetivoSegundos,
                Observaciones = e.Observaciones
            }).ToList(),
            AdvertenciasLimites = _validador.Validar(p, p.Socio?.Nivel)
                .Select(v => new ViolacionLimiteDto { Codigo = v.Codigo, Mensaje = v.Mensaje, Orden = v.Orden }).ToList()
        };
    }
}