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
        /// Equipamiento de un gimnasio. Administrador de la Plataforma: el de gimnasioId (obligatorio), solo lectura.
        /// Dueño y Personal Trainer: el propio (si se indica otro gimnasio, 404).
        /// </summary>
        Task<EquipamientoGimnasioDto> ObtenerAsync(Guid? gimnasioId);

        /// <summary>Reemplaza el conjunto de tipos marcados del gimnasio del Dueño.</summary>
        Task<EquipamientoGimnasioDto> ReemplazarAsync(EquipamientoReemplazoRequest request);

        /// <summary>Crea un tipo propio del gimnasio del Dueño (sin marcarlo).</summary>
        Task<EquipamientoItemDto> CrearTipoAsync(TipoEquipamientoCreateRequest request);
    }

    /// <summary>Equipamiento declarado por el gimnasio (Etapa 9a-2).</summary>
    public class EquipamientoService : IEquipamientoService
    {
        public const int LargoNombreTipo = 80;
        public const int CantidadMaxima = 10000;

        private readonly IEquipamientoRepository _repo;
        private readonly IGimnasioRepository _gimnasios;
        private readonly IUsuarioActual _actual;
        private readonly IAuditService _audit;

        public EquipamientoService(IEquipamientoRepository repo, IGimnasioRepository gimnasios, IUsuarioActual actual, IAuditService audit)
        {
            _repo = repo;
            _gimnasios = gimnasios;
            _actual = actual;
            _audit = audit;
        }

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
            var gimnasioId = _actual.GimnasioId ?? throw new AccesoDenegadoException();
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
            await _audit.RegistrarAsync("CAMBIO_EQUIPAMIENTO", nameof(EquipamientoGimnasio), gimnasioId.ToString(),
                $"{aMarcar.Count} tipos marcados");

            var gimnasio = await _gimnasios.GetByIdAsync(gimnasioId);
            return await ArmarAsync(gimnasioId, gimnasio?.Nombre ?? string.Empty);
        }

        public async Task<EquipamientoItemDto> CrearTipoAsync(TipoEquipamientoCreateRequest request)
        {
            var gimnasioId = _actual.GimnasioId ?? throw new AccesoDenegadoException();
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
            await _audit.RegistrarAsync("ALTA_TIPO_EQUIPAMIENTO", nameof(TipoEquipamiento), tipo.Id.ToString());
            return Item(tipo, null);
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
                Editable = _actual.Rol == RolUsuario.AdministradorGimnasio && _actual.GimnasioId == gimnasioId,
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
