using GymBro.Application.DTOs;
using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Domain.Exceptions;
using GymBro.Domain.Repositories;
using GymBro.Domain.Rules;
using GymBro.Domain.Security;
using Pgvector;

namespace GymBro.Application.Services
{
    public interface IEjercicioGimnasioService
    {
        Task<EjercicioDetalleDto> CrearAsync(EjercicioGimnasioRequest request);
        Task<EjercicioDetalleDto> EditarAsync(Guid id, EjercicioGimnasioRequest request);
        Task DarDeBajaAsync(Guid id);
    }

    /// <summary>
    /// Ejercicios propios del gimnasio (Etapa 9a-2): alta, edición y baja lógica por el Dueño.
    /// El gimnasio sale del JWT y el origen lo fija el servidor. Criterio de rechazo (T7 de la 8c): 404 si el ejercicio
    /// no es visible para quien llama; 403 si es visible pero no se puede modificar (por ejemplo, uno global).
    ///
    /// ADVERTENCIA: estos ejercicios no pasan por ninguna validación profesional ni de seguridad y el baseline los puede
    /// prescribir. Queda como insumo de las reglas de seguridad de la 10a.
    /// </summary>
    public class EjercicioGimnasioService : IEjercicioGimnasioService
    {
        public const int DificultadMinima = 1;
        /// <summary>Rango real verificado en la base local el 18/11/2026 (dataset: 1 a 5).</summary>
        public const int DificultadMaxima = 5;

        private readonly IEjercicioRepository _ejercicios;
        private readonly IEquipamientoRepository _equipamiento;
        private readonly IIaService _ia;
        private readonly IUsuarioActual _actual;
        private readonly IAuditService _audit;
        private readonly EmbeddingConfig _config;

        public EjercicioGimnasioService(IEjercicioRepository ejercicios, IEquipamientoRepository equipamiento, IIaService ia,
            IUsuarioActual actual, IAuditService audit, EmbeddingConfig config)
        {
            _ejercicios = ejercicios;
            _equipamiento = equipamiento;
            _ia = ia;
            _actual = actual;
            _audit = audit;
            _config = config;
        }

        public async Task<EjercicioDetalleDto> CrearAsync(EjercicioGimnasioRequest request)
        {
            var gimnasioId = _actual.GimnasioId ?? throw new AccesoDenegadoException();
            var datos = await ValidarAsync(request, gimnasioId, ejercicioId: null, tipoActual: null);

            var ejercicio = new Ejercicio
            {
                Id = Guid.NewGuid(),
                Origen = OrigenEjercicio.Gimnasio,
                GimnasioId = gimnasioId,
            };
            Aplicar(ejercicio, datos);
            ejercicio.Embedding = await GenerarEmbeddingAsync(ejercicio);

            await _ejercicios.AddAsync(ejercicio);
            await _ejercicios.SaveChangesAsync();
            await _audit.RegistrarAsync("ALTA_EJERCICIO", nameof(Ejercicio), ejercicio.Id.ToString(),
                ejercicio.Embedding is null ? "Embedding pendiente" : null);
            return Mapear(ejercicio, datos.Tipo.Nombre);
        }

        public async Task<EjercicioDetalleDto> EditarAsync(Guid id, EjercicioGimnasioRequest request)
        {
            var (ejercicio, gimnasioId) = await ObtenerPropioAsync(id);
            var datos = await ValidarAsync(request, gimnasioId, id, ejercicio.TipoEquipamientoId);

            var cambiaTexto = ejercicio.Nombre != datos.Nombre || ejercicio.ZonaObjetivo != datos.Zona
                              || ejercicio.MusculoPrincipal != datos.Musculo || ejercicio.Ejecucion != datos.Ejecucion;
            Aplicar(ejercicio, datos);
            ejercicio.FechaModificacion = DateTime.UtcNow;
            // Si cambia el texto que se vectoriza, se recalcula; si falla, queda en null (nunca un vector de un texto anterior).
            if (cambiaTexto) ejercicio.Embedding = await GenerarEmbeddingAsync(ejercicio);

            await _ejercicios.SaveChangesAsync();
            await _audit.RegistrarAsync("EDICION_EJERCICIO", nameof(Ejercicio), ejercicio.Id.ToString(),
                cambiaTexto && ejercicio.Embedding is null ? "Embedding pendiente" : null);
            return Mapear(ejercicio, datos.Tipo.Nombre);
        }

        public async Task DarDeBajaAsync(Guid id)
        {
            var (ejercicio, _) = await ObtenerPropioAsync(id);
            if (await _ejercicios.EnUsoPorPrescripcionVigenteAsync(id))
                throw new ReglaNegocioException(
                    "El ejercicio está en una rutina vigente o pendiente de revisión: no se puede dar de baja.");

            ejercicio.FechaBaja = DateTime.UtcNow;
            await _ejercicios.SaveChangesAsync();
            await _audit.RegistrarAsync("BAJA_EJERCICIO", nameof(Ejercicio), ejercicio.Id.ToString());
        }

        /// <summary>404 si no es visible; 403 si es visible pero no es un ejercicio propio de este gimnasio.</summary>
        private async Task<(Ejercicio Ejercicio, Guid GimnasioId)> ObtenerPropioAsync(Guid id)
        {
            var gimnasioId = _actual.GimnasioId ?? throw new AccesoDenegadoException();
            var ejercicio = await _ejercicios.GetParaEdicionAsync(id)
                ?? throw new RecursoNoEncontradoException("Ejercicio no encontrado.");
            if (ejercicio.Origen != OrigenEjercicio.Gimnasio || ejercicio.GimnasioId != gimnasioId)
                throw new AccesoDenegadoException("Solo se pueden modificar los ejercicios propios del gimnasio.");
            return (ejercicio, gimnasioId);
        }

        private sealed record Datos(string Nombre, string Zona, TipoEquipamiento Tipo, string Musculo, string Sinergistas,
            string Estabilizadores, string Preparacion, string Ejecucion, TipoMedida Medida, int Dificultad);

        private async Task<Datos> ValidarAsync(EjercicioGimnasioRequest r, Guid gimnasioId, Guid? ejercicioId, Guid? tipoActual)
        {
            var nombre = TextoValidado.Validar(r.Nombre, "El nombre", TextoValidado.LargoNombre, obligatorio: true);
            var musculo = TextoValidado.Validar(r.MusculoPrincipal, "El músculo principal", TextoValidado.LargoNombre, obligatorio: true);
            var sinergistas = TextoValidado.Validar(r.Sinergistas, "Los sinergistas", TextoValidado.LargoTextoLibre, false, true);
            var estabilizadores = TextoValidado.Validar(r.Estabilizadores, "Los estabilizadores", TextoValidado.LargoTextoLibre, false, true);
            var preparacion = TextoValidado.Validar(r.Preparacion, "La preparación", TextoValidado.LargoTextoLibre, false, true);
            var ejecucion = TextoValidado.Validar(r.Ejecucion, "La ejecución", TextoValidado.LargoTextoLibre, true, true);

            if (r.Dificultad < DificultadMinima || r.Dificultad > DificultadMaxima)
                throw new ReglaNegocioException($"La dificultad debe estar entre {DificultadMinima} y {DificultadMaxima}.");

            if (string.IsNullOrWhiteSpace(r.TipoMedida) || int.TryParse(r.TipoMedida, out _)
                || !Enum.TryParse<TipoMedida>(r.TipoMedida.Trim(), true, out var medida) || !Enum.IsDefined(medida))
                throw new ReglaNegocioException("El tipo de medida debe ser Repeticiones o Segundos.");

            var zonas = await _ejercicios.GetZonasObjetivoAsync();
            var zona = zonas.FirstOrDefault(z => string.Equals(z, r.ZonaObjetivo?.Trim(), StringComparison.OrdinalIgnoreCase))
                       ?? throw new ReglaNegocioException("La zona objetivo no existe en el catálogo.");

            var tipo = await _equipamiento.GetTipoAsync(r.TipoEquipamientoId);
            if (tipo is null || (tipo.GimnasioId is not null && tipo.GimnasioId != gimnasioId))
                throw new ReglaNegocioException("El tipo de equipamiento no existe.");
            // El equipamiento debe estar disponible en el gimnasio. Al editar sin cambiar el tipo no se vuelve a exigir.
            if (!tipo.SiempreDisponible && tipo.Id != tipoActual)
            {
                var marcados = await _equipamiento.GetMarcadosAsync(gimnasioId);
                if (marcados.All(m => m.TipoEquipamientoId != tipo.Id))
                    throw new ReglaNegocioException("El gimnasio no tiene declarado ese equipamiento.");
            }

            var clave = CatalogoEquipamiento.ClaveDeComparacion(nombre);
            var nombres = await _ejercicios.GetNombresVisiblesAsync(gimnasioId);
            if (nombres.Any(n => n.Id != ejercicioId && CatalogoEquipamiento.ClaveDeComparacion(n.Nombre) == clave))
                throw new ReglaNegocioException("Ya existe un ejercicio con ese nombre.");

            return new Datos(nombre, zona, tipo, musculo, sinergistas, estabilizadores, preparacion, ejecucion, medida, r.Dificultad);
        }

        private static void Aplicar(Ejercicio e, Datos d)
        {
            e.Nombre = d.Nombre;
            e.ZonaObjetivo = d.Zona;
            e.TipoEquipamientoId = d.Tipo.Id;
            // Se copia el nombre del tipo al texto libre para no romper lo que ya lo usa (búsqueda, preferencia de mancuerna).
            e.Equipamiento = d.Tipo.Nombre;
            e.MusculoPrincipal = d.Musculo;
            e.Sinergistas = d.Sinergistas;
            e.Estabilizadores = d.Estabilizadores;
            e.Preparacion = d.Preparacion;
            e.Ejecucion = d.Ejecucion;
            e.TipoMedida = d.Medida;
            e.Dificultad = d.Dificultad;
        }

        /// <summary>
        /// Embedding del texto semántico. Si el servicio de IA falla, tarda más que el máximo configurado o devuelve un
        /// vector de otra dimensión, se devuelve null: el ejercicio queda pendiente de indexar (generador_embeddings.py
        /// procesa los que tienen embedding IS NULL).
        /// </summary>
        private async Task<Vector?> GenerarEmbeddingAsync(Ejercicio e)
        {
            var texto = EjercicioTextoSemantico.Componer(e.Nombre, e.ZonaObjetivo, e.MusculoPrincipal, e.Ejecucion);
            try
            {
                var vector = await _ia.GenerarEmbeddingAsync(texto)
                    .WaitAsync(TimeSpan.FromSeconds(Math.Max(1, _config.TiempoMaximoSegundos)));
                return vector is { Length: EmbeddingConfig.Dimension } ? new Vector(vector) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static EjercicioDetalleDto Mapear(Ejercicio e, string tipoNombre) => new()
        {
            Id = e.Id,
            Nombre = e.Nombre,
            Equipamiento = e.Equipamiento,
            ZonaObjetivo = e.ZonaObjetivo,
            MusculoPrincipal = e.MusculoPrincipal,
            Sinergistas = e.Sinergistas,
            Estabilizadores = e.Estabilizadores,
            Preparacion = e.Preparacion,
            Ejecucion = e.Ejecucion,
            Dificultad = e.Dificultad,
            TipoMedida = e.TipoMedida.ToString(),
            Origen = e.Origen.ToString(),
            GimnasioId = e.GimnasioId,
            TipoEquipamientoId = e.TipoEquipamientoId,
            TipoEquipamiento = tipoNombre,
        };
    }
}
