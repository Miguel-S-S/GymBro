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

        /// <summary>Desactiva (false) o reactiva (true) un ejercicio. Etapa 9a-2b: reemplaza a la baja.</summary>
        Task<EjercicioDetalleDto> CambiarEstadoAsync(Guid id, bool activo);
    }

    /// <summary>
    /// Ejercicios propios del gimnasio (Etapa 9a-2, ampliada en la 9a-2b). Quién puede hacer qué:
    ///   - Administrador de la Plataforma: alta en cualquier gimnasio (indicado en el cuerpo y validado), edición y
    ///     desactivación de los propios de cualquier gimnasio; desactivar y reactivar los globales. Los globales (Dataset y
    ///     Proyecto) no se editan por la API: se modifican por script (docs/pfc/scripts/admin-ejercicio-global.sql).
    ///   - Dueño: alta, edición y desactivacion de cualquier ejercicio propio de su gimnasio.
    ///   - Personal Trainer (D4): alta en su gimnasio; edita y desactiva solo los que él creó.
    /// El gimnasio del Dueño y del Personal Trainer sale del JWT; si el cuerpo indica otro, 404 (no sirve para saltarse el
    /// aislamiento). El origen y el autor los fija el servidor. Criterio de rechazo (T7 de la 8c): 404 si el ejercicio no
    /// es visible para quien llama; 403 si es visible pero no lo puede modificar; 400 por regla de negocio.
    /// Etapa 9a-2b: los ejercicios no se dan de baja; se desactivan y se pueden reactivar.
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
        private readonly IGimnasioRepository _gimnasios;
        private readonly IIaService _ia;
        private readonly IUsuarioActual _actual;
        private readonly IAuditService _audit;
        private readonly EmbeddingConfig _config;

        public EjercicioGimnasioService(IEjercicioRepository ejercicios, IEquipamientoRepository equipamiento,
            IGimnasioRepository gimnasios, IIaService ia, IUsuarioActual actual, IAuditService audit, EmbeddingConfig config)
        {
            _ejercicios = ejercicios;
            _equipamiento = equipamiento;
            _gimnasios = gimnasios;
            _ia = ia;
            _actual = actual;
            _audit = audit;
            _config = config;
        }

        private bool EsPlataforma => _actual.Rol == RolUsuario.SuperAdministrador;

        public async Task<EjercicioDetalleDto> CrearAsync(EjercicioGimnasioRequest request)
        {
            var gimnasioId = await GimnasioDestino.ResolverAsync(_actual, _gimnasios, request.GimnasioId);
            var datos = await ValidarAsync(request, gimnasioId, ejercicioId: null, tipoActual: null);

            var ejercicio = new Ejercicio
            {
                Id = Guid.NewGuid(),
                Origen = OrigenEjercicio.Gimnasio,
                GimnasioId = gimnasioId,
                // Sale del JWT; el cuerpo no lo puede fijar.
                CreadoPorUsuarioId = _actual.UsuarioId,
            };
            Aplicar(ejercicio, datos);
            ejercicio.Embedding = await GenerarEmbeddingAsync(ejercicio);

            await _ejercicios.AddAsync(ejercicio);
            await _ejercicios.SaveChangesAsync();
            await _audit.RegistrarEnGimnasioAsync("ALTA_EJERCICIO", nameof(Ejercicio), ejercicio.Id.ToString(), gimnasioId,
                ejercicio.Embedding is null ? "Embedding pendiente" : null);
            return await DetalleAsync(ejercicio.Id);
        }

        public async Task<EjercicioDetalleDto> EditarAsync(Guid id, EjercicioGimnasioRequest request)
        {
            var ejercicio = await ObtenerParaGestionAsync(id, operacionSobreGlobal: false);
            var gimnasioId = ejercicio.GimnasioId!.Value;
            var datos = await ValidarAsync(request, gimnasioId, id, ejercicio.TipoEquipamientoId);

            var cambiaTexto = ejercicio.Nombre != datos.Nombre || ejercicio.ZonaObjetivo != datos.Zona
                              || ejercicio.MusculoPrincipal != datos.Musculo || ejercicio.Ejecucion != datos.Ejecucion;
            Aplicar(ejercicio, datos);
            ejercicio.FechaModificacion = DateTime.UtcNow;
            // Si cambia el texto que se vectoriza, se recalcula; si falla, queda en null (nunca un vector de un texto anterior).
            if (cambiaTexto) ejercicio.Embedding = await GenerarEmbeddingAsync(ejercicio);

            await _ejercicios.SaveChangesAsync();
            await _audit.RegistrarEnGimnasioAsync("EDICION_EJERCICIO", nameof(Ejercicio), ejercicio.Id.ToString(), gimnasioId,
                cambiaTexto && ejercicio.Embedding is null ? "Embedding pendiente" : null);
            return await DetalleAsync(ejercicio.Id);
        }

        public async Task<EjercicioDetalleDto> CambiarEstadoAsync(Guid id, bool activo)
        {
            var ejercicio = await ObtenerParaGestionAsync(id, operacionSobreGlobal: true);
            if (ejercicio.Activo != activo)
            {
                // Integridad: no se desactiva un ejercicio de una rutina vigente o pendiente, tampoco el Administrador.
                if (!activo && await _ejercicios.EnUsoPorPrescripcionVigenteAsync(id))
                    throw new ReglaNegocioException(
                        "El ejercicio está en una rutina vigente o pendiente de revisión: no se puede desactivar.");

                ejercicio.Activo = activo;
                ejercicio.FechaModificacion = DateTime.UtcNow;
                await _ejercicios.SaveChangesAsync();
                await _audit.RegistrarEnGimnasioAsync(activo ? "REACTIVAR_EJERCICIO" : "DESACTIVAR_EJERCICIO", nameof(Ejercicio),
                    ejercicio.Id.ToString(), ejercicio.GimnasioId);
            }
            return await DetalleAsync(ejercicio.Id);
        }

        /// <summary>
        /// Ejercicio para gestionar (incluidos los desactivados). 404 si no es visible para quien llama; 403 si es visible
        /// pero no lo puede modificar. Los globales solo los desactiva o reactiva el Administrador de la Plataforma.
        /// </summary>
        private async Task<Ejercicio> ObtenerParaGestionAsync(Guid id, bool operacionSobreGlobal)
        {
            var ejercicio = await _ejercicios.GetParaGestionAsync(id);
            if (ejercicio is null || !EsVisible(ejercicio))
                throw new RecursoNoEncontradoException("Ejercicio no encontrado.");

            if (ejercicio.GimnasioId is null)
            {
                if (EsPlataforma && operacionSobreGlobal) return ejercicio;
                throw new AccesoDenegadoException(EsPlataforma
                    ? "Los ejercicios globales no se editan desde la aplicación: se modifican por script."
                    : "Solo se pueden modificar los ejercicios propios del gimnasio.");
            }

            if (_actual.Rol == RolUsuario.Entrenador && ejercicio.CreadoPorUsuarioId != _actual.UsuarioId)
                throw new AccesoDenegadoException("El Personal Trainer solo modifica los ejercicios que él creó.");
            return ejercicio;
        }

        /// <summary>
        /// Misma regla que el filtro global, pero alcanzando también a los desactivados: el Administrador de la Plataforma
        /// ve todos; el personal del gimnasio, los globales activos y los propios de su gimnasio.
        /// </summary>
        private bool EsVisible(Ejercicio e)
        {
            if (EsPlataforma) return true;
            if (_actual.GimnasioId is null) return false;
            return e.GimnasioId is null ? e.Activo : e.GimnasioId == _actual.GimnasioId;
        }

        private async Task<EjercicioDetalleDto> DetalleAsync(Guid id)
        {
            var e = await _ejercicios.GetByIdAsync(id, EjercicioCatalogoService.AlcanceDe(_actual))
                    ?? throw new RecursoNoEncontradoException("Ejercicio no encontrado.");
            return Mapear(e);
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

        private EjercicioDetalleDto Mapear(Ejercicio e) => new()
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
            TipoEquipamiento = e.TipoEquipamiento?.Nombre,
            Activo = e.Activo,
            CreadoPorUsuarioId = e.CreadoPorUsuarioId,
            CreadoPor = e.CreadoPor is null ? null
                : e.CreadoPor.Rol == RolUsuario.SuperAdministrador && !EsPlataforma ? "Administrador de la Plataforma"
                : e.CreadoPor.NombreCompleto,
            PuedeEditar = EjercicioCatalogoService.Permisos(_actual, e).Editar,
            PuedeCambiarEstado = EjercicioCatalogoService.Permisos(_actual, e).CambiarEstado,
        };
    }
}
