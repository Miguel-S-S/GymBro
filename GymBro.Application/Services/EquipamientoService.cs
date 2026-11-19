using System.Text;
using GymBro.Application.DTOs;
using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Domain.Exceptions;
using GymBro.Domain.Repositories;
using GymBro.Domain.Rules;
using GymBro.Domain.Security;

namespace GymBro.Application.Services
{
    public interface IEquipamientoService
    {
        /// <summary>
        /// Equipamiento de un gimnasio. Administrador de la Plataforma: el de gimnasioId (obligatorio).
        /// Dueño y Personal Trainer: el propio (si se indica otro gimnasio, 404).
        /// </summary>
        Task<EquipamientoGimnasioDto> ObtenerAsync(Guid? gimnasioId);

        /// <summary>
        /// Reemplaza el conjunto de tipos marcados. Dueño: su gimnasio. Administrador de la Plataforma: el gimnasio indicado
        /// en el pedido (Etapa 9a-2b).
        /// </summary>
        Task<EquipamientoGimnasioDto> ReemplazarAsync(EquipamientoReemplazoRequest request);

        /// <summary>
        /// Etapa 9a-2b (O1): qué ejercicios propios activos del gimnasio dejarían de ser factibles con ese reemplazo.
        /// Solo lectura: no modifica datos.
        /// </summary>
        Task<EquipamientoPrevisualizacionDto> PrevisualizarAsync(EquipamientoReemplazoRequest request);

        /// <summary>Crea un tipo propio (sin marcarlo) en el gimnasio del Dueño o en el indicado por la plataforma.</summary>
        Task<EquipamientoItemDto> CrearTipoAsync(TipoEquipamientoCreateRequest request);

        /// <summary>
        /// Etapa 9a-2b: renombra un tipo (la clave no cambia). Dueño: los propios de su gimnasio. Administrador de la
        /// Plataforma: propios de cualquier gimnasio y globales.
        /// </summary>
        Task<EquipamientoItemDto> EditarTipoAsync(Guid id, TipoEquipamientoEditRequest request);

        /// <summary>
        /// Etapa 9a-2b: baja lógica de un tipo (mismos permisos que la edición). Se rechaza si algún ejercicio, activo o
        /// desactivado, lo usa; al darlo de baja se desmarca en los gimnasios que lo tenían.
        /// </summary>
        Task DarDeBajaTipoAsync(Guid id);
    }

    /// <summary>
    /// Equipamiento declarado por el gimnasio (Etapa 9a-2). Etapa 9a-2b: el Administrador de la Plataforma también escribe,
    /// indicando el gimnasio (GimnasioDestino); cada operación se audita con el gimnasio afectado.
    /// </summary>
    public class EquipamientoService : IEquipamientoService
    {
        public const int LargoNombreTipo = 80;
        public const int CantidadMaxima = 10000;

        /// <summary>Máximo de nombres de ejercicios que devuelve la previsualización.</summary>
        public const int MaximoNombresPrevisualizacion = 10;

        private readonly IEquipamientoRepository _repo;
        private readonly IGimnasioRepository _gimnasios;
        private readonly IEjercicioRepository _ejercicios;
        private readonly IUsuarioActual _actual;
        private readonly IAuditService _audit;

        public EquipamientoService(IEquipamientoRepository repo, IGimnasioRepository gimnasios, IEjercicioRepository ejercicios,
            IUsuarioActual actual, IAuditService audit)
        {
            _repo = repo;
            _gimnasios = gimnasios;
            _ejercicios = ejercicios;
            _actual = actual;
            _audit = audit;
        }

        private bool EsPlataforma => _actual.Rol == RolUsuario.SuperAdministrador;

        public async Task<EquipamientoGimnasioDto> ObtenerAsync(Guid? gimnasioId)
        {
            Guid id;
            if (_actual.Rol == RolUsuario.SuperAdministrador)
                id = gimnasioId ?? throw new ReglaNegocioException("Indique el gimnasio a consultar.");
            else
            {
                id = _actual.GimnasioId ?? throw new AccesoDenegadoException();
                if (gimnasioId is not null && gimnasioId != id)
                    throw new RecursoNoEncontradoException("Gimnasio no encontrado.");
            }

            var gimnasio = await _gimnasios.GetByIdAsync(id) ?? throw new RecursoNoEncontradoException("Gimnasio no encontrado.");
            return await ArmarAsync(id, gimnasio.Nombre);
        }

        public async Task<EquipamientoGimnasioDto> ReemplazarAsync(EquipamientoReemplazoRequest request)
        {
            var gimnasioId = await GimnasioDestino.ResolverAsync(_actual, _gimnasios, request.GimnasioId);
            var aMarcar = await ValidarPedidoAsync(request, gimnasioId);

            var ahora = DateTime.UtcNow;
            var vigentes = await _repo.GetMarcadosAsync(gimnasioId);
            foreach (var v in vigentes)
            {
                if (!aMarcar.TryGetValue(v.TipoEquipamientoId, out var cantidad))
                    v.FechaBaja = ahora; // quitar = baja lógica
                else if (v.Cantidad != cantidad)
                {
                    v.Cantidad = cantidad;
                    v.FechaModificacion = ahora;
                }
            }
            // Volver a marcar un tipo inserta una fila nueva (la dada de baja queda como historial).
            foreach (var (tipoId, cantidad) in aMarcar.Where(m => vigentes.All(v => v.TipoEquipamientoId != m.Key)))
                await _repo.AddMarcadoAsync(new EquipamientoGimnasio { GimnasioId = gimnasioId, TipoEquipamientoId = tipoId, Cantidad = cantidad });

            await _repo.SaveChangesAsync();
            await _audit.RegistrarEnGimnasioAsync("CAMBIO_EQUIPAMIENTO", nameof(EquipamientoGimnasio), gimnasioId.ToString(), gimnasioId,
                $"{aMarcar.Count} tipos marcados");

            var gimnasio = await _gimnasios.GetByIdAsync(gimnasioId);
            return await ArmarAsync(gimnasioId, gimnasio?.Nombre ?? string.Empty);
        }

        public async Task<EquipamientoPrevisualizacionDto> PrevisualizarAsync(EquipamientoReemplazoRequest request)
        {
            var gimnasioId = await GimnasioDestino.ResolverAsync(_actual, _gimnasios, request.GimnasioId);
            var aMarcar = await ValidarPedidoAsync(request, gimnasioId);

            // Tipos marcados hoy que el pedido quita (los siempre disponibles nunca se guardan, así que no aparecen).
            var quitados = (await _repo.GetMarcadosAsync(gimnasioId))
                .Where(v => v.FechaBaja == null && !aMarcar.ContainsKey(v.TipoEquipamientoId))
                .Select(v => v.TipoEquipamientoId)
                .Distinct()
                .ToList();
            var nombresTipos = (await _repo.GetTiposParaGimnasioAsync(gimnasioId)).ToDictionary(t => t.Id, t => t.Nombre);
            var afectados = await _ejercicios.GetPropiosActivosPorTiposAsync(gimnasioId, quitados);

            return new EquipamientoPrevisualizacionDto
            {
                Cantidad = afectados.Count,
                Ejercicios = afectados.Take(MaximoNombresPrevisualizacion).ToList(),
                TiposQuitados = quitados.Select(id => nombresTipos.GetValueOrDefault(id, string.Empty))
                    .Where(n => n.Length > 0).OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToList(),
            };
        }

        /// <summary>Valida el pedido y devuelve los tipos a marcar (sin los siempre disponibles) con su cantidad.</summary>
        private async Task<Dictionary<Guid, int?>> ValidarPedidoAsync(EquipamientoReemplazoRequest request, Guid gimnasioId)
        {
            var pedidos = request.Items ?? new List<EquipamientoMarcadoRequest>();
            if (pedidos.Select(p => p.TipoEquipamientoId).Distinct().Count() != pedidos.Count)
                throw new ReglaNegocioException("Hay tipos de equipamiento repetidos.");

            var tipos = (await _repo.GetTiposParaGimnasioAsync(gimnasioId)).ToDictionary(t => t.Id);
            var aMarcar = new Dictionary<Guid, int?>();
            foreach (var p in pedidos)
            {
                // Un tipo de otro gimnasio no es visible: se rechaza igual que uno inexistente.
                if (!tipos.TryGetValue(p.TipoEquipamientoId, out var tipo))
                    throw new ReglaNegocioException("Tipo de equipamiento inexistente.");
                if (p.Cantidad is not null && (p.Cantidad < 1 || p.Cantidad > CantidadMaxima))
                    throw new ReglaNegocioException($"La cantidad debe estar entre 1 y {CantidadMaxima}.");
                // Los siempre disponibles no se guardan: están incluidos en cualquier gimnasio.
                if (!tipo.SiempreDisponible) aMarcar[tipo.Id] = p.Cantidad;
            }
            return aMarcar;
        }

        public async Task<EquipamientoItemDto> CrearTipoAsync(TipoEquipamientoCreateRequest request)
        {
            var gimnasioId = await GimnasioDestino.ResolverAsync(_actual, _gimnasios, request.GimnasioId);
            var nombre = TextoValidado.Validar(request.Nombre, "El nombre", LargoNombreTipo, obligatorio: true);
            var comparacion = CatalogoEquipamiento.ClaveDeComparacion(nombre);

            var tipos = await _repo.GetTiposParaGimnasioAsync(gimnasioId);
            if (tipos.Any(t => CatalogoEquipamiento.ClaveDeComparacion(t.Nombre) == comparacion))
                throw new ReglaNegocioException("Ya existe un tipo de equipamiento con ese nombre.");

            var baseClave = GenerarClave(comparacion);
            var clave = baseClave;
            for (var i = 2; await _repo.ExisteClavePropiaAsync(gimnasioId, clave); i++) clave = $"{baseClave}_{i}";

            var tipo = new TipoEquipamiento { Clave = clave, Nombre = nombre, SiempreDisponible = false, GimnasioId = gimnasioId };
            await _repo.AddTipoAsync(tipo);
            await _repo.SaveChangesAsync();
            await _audit.RegistrarEnGimnasioAsync("ALTA_TIPO_EQUIPAMIENTO", nameof(TipoEquipamiento), tipo.Id.ToString(), gimnasioId);
            return Item(tipo, null);
        }

        public async Task<EquipamientoItemDto> EditarTipoAsync(Guid id, TipoEquipamientoEditRequest request)
        {
            var tipo = await ObtenerTipoParaGestionAsync(id);
            var nombre = TextoValidado.Validar(request.Nombre, "El nombre", LargoNombreTipo, obligatorio: true);
            var comparacion = CatalogoEquipamiento.ClaveDeComparacion(nombre);

            // Unicidad: un tipo propio no puede repetir un global ni otro propio de su gimnasio; un global no puede repetir
            // ningún tipo vigente (global o propio de cualquier gimnasio), porque aparece en todos.
            var competidores = tipo.GimnasioId is { } g
                ? await _repo.GetTiposParaGimnasioAsync(g)
                : await _repo.GetTodosLosTiposVigentesAsync();
            if (competidores.Any(t => t.Id != tipo.Id && CatalogoEquipamiento.ClaveDeComparacion(t.Nombre) == comparacion))
                throw new ReglaNegocioException("Ya existe un tipo de equipamiento con ese nombre.");

            if (tipo.Nombre != nombre)
            {
                tipo.Nombre = nombre;
                tipo.FechaModificacion = DateTime.UtcNow;
                await _repo.SaveChangesAsync();
                // Los ejercicios propios copian el nombre del tipo en su texto de equipamiento (9a-2): se mantiene coherente.
                // Los globales del dataset conservan su texto original.
                await _ejercicios.ActualizarTextoEquipamientoAsync(tipo.Id, nombre);
                await _audit.RegistrarEnGimnasioAsync("EDICION_TIPO_EQUIPAMIENTO", nameof(TipoEquipamiento), tipo.Id.ToString(), tipo.GimnasioId);
            }
            return Item(tipo, null);
        }

        public async Task DarDeBajaTipoAsync(Guid id)
        {
            var tipo = await ObtenerTipoParaGestionAsync(id);
            var enUso = await _ejercicios.ContarPorTipoAsync(tipo.Id);
            if (enUso > 0)
                throw new ReglaNegocioException(
                    $"El tipo lo usan {enUso} ejercicios (activos o desactivados): no se puede dar de baja.");

            var ahora = DateTime.UtcNow;
            tipo.FechaBaja = ahora;
            var marcados = await _repo.GetMarcadosDeTipoAsync(tipo.Id);
            foreach (var m in marcados) m.FechaBaja = ahora;
            await _repo.SaveChangesAsync();
            await _audit.RegistrarEnGimnasioAsync("BAJA_TIPO_EQUIPAMIENTO", nameof(TipoEquipamiento), tipo.Id.ToString(), tipo.GimnasioId,
                $"{marcados.Count} gimnasios lo tenían marcado");
        }

        /// <summary>
        /// 404 si el tipo no es visible (filtro de EF: el Dueño ve los globales y los de su gimnasio); 403 si es un global y
        /// quien llama no es el Administrador de la Plataforma.
        /// </summary>
        private async Task<TipoEquipamiento> ObtenerTipoParaGestionAsync(Guid id)
        {
            var tipo = await _repo.GetTipoParaGestionAsync(id)
                       ?? throw new RecursoNoEncontradoException("Tipo de equipamiento no encontrado.");
            if (tipo.GimnasioId is null && !EsPlataforma)
                throw new AccesoDenegadoException("Los tipos globales solo los modifica el Administrador de la Plataforma.");
            if (tipo.GimnasioId is not null && !EsPlataforma && tipo.GimnasioId != _actual.GimnasioId)
                throw new RecursoNoEncontradoException("Tipo de equipamiento no encontrado.");
            return tipo;
        }

        private async Task<EquipamientoGimnasioDto> ArmarAsync(Guid gimnasioId, string nombreGimnasio)
        {
            var tipos = await _repo.GetTiposParaGimnasioAsync(gimnasioId);
            var marcados = (await _repo.GetMarcadosAsync(gimnasioId)).Where(m => m.FechaBaja == null)
                .ToDictionary(m => m.TipoEquipamientoId);
            var ordenGlobal = CatalogoEquipamiento.TiposGlobales().Select((t, i) => (t.Id, i)).ToDictionary(x => x.Id, x => x.i);

            return new EquipamientoGimnasioDto
            {
                GimnasioId = gimnasioId,
                NombreGimnasio = nombreGimnasio,
                Editable = EsPlataforma || (_actual.Rol == RolUsuario.AdministradorGimnasio && _actual.GimnasioId == gimnasioId),
                Items = tipos
                    .OrderBy(t => t.GimnasioId is null ? 0 : 1)
                    .ThenBy(t => ordenGlobal.TryGetValue(t.Id, out var i) ? i : int.MaxValue)
                    .ThenBy(t => t.Nombre, StringComparer.CurrentCultureIgnoreCase)
                    .Select(t => Item(t, marcados.GetValueOrDefault(t.Id)))
                    .ToList()
            };
        }

        private static EquipamientoItemDto Item(TipoEquipamiento t, EquipamientoGimnasio? marcado) => new()
        {
            TipoEquipamientoId = t.Id,
            Clave = t.Clave,
            Nombre = t.Nombre,
            SiempreDisponible = t.SiempreDisponible,
            Propio = t.GimnasioId is not null,
            Marcado = t.SiempreDisponible || marcado is not null,
            Cantidad = marcado?.Cantidad,
        };

        /// <summary>Clave estable a partir del nombre: GIM_ + letras y dígitos en mayúsculas (sin acentos), hasta 40 caracteres.</summary>
        public static string GenerarClave(string nombreComparacion)
        {
            var sb = new StringBuilder("GIM_");
            var guionPendiente = false;
            foreach (var c in nombreComparacion.ToUpperInvariant())
            {
                if (c is >= 'A' and <= 'Z' or >= '0' and <= '9')
                {
                    if (guionPendiente && sb.Length > 4) sb.Append('_');
                    guionPendiente = false;
                    sb.Append(c);
                }
                else guionPendiente = true;
            }
            if (sb.Length == 4) sb.Append("TIPO");
            return sb.Length > 44 ? sb.ToString(0, 44) : sb.ToString();
        }
    }
}
