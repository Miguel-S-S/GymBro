using GymBro.Domain.Entities;
using GymBro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GymBro.Infrastructure.Repositories
{
    /// <summary>
    /// Etapa 9a-2 (riesgo del historial, aprobado el 18/11/2026): con el filtro de ejercicios por gimnasio, una rutina o
    /// una sesión ya autorizada puede referir a un ejercicio que el filtro oculta (ejercicio propio dado de baja, o de un
    /// gimnasio al que el socio ya no pertenece). Para no perder el nombre en el historial, esos ejercicios se completan
    /// con una consulta aparte que ignora el filtro, limitada a los Id que la rutina o la sesión ya referencian.
    /// No expone nada nuevo: el acceso a la rutina o a la sesión ya se validó.
    ///
    /// Por eso las consultas de historial NO usan ThenInclude(Ejercicio): la navegación es obligatoria y EF la resuelve
    /// con INNER JOIN, así que un ejercicio oculto por el filtro hacía desaparecer la fila entera de la rutina o la sesión
    /// (verificado con la prueba de integración El_historial_conserva_el_nombre...).
    /// </summary>
    internal static class EjerciciosDeHistorial
    {
        public static Task CompletarAsync(GymBroDbContext context, IEnumerable<PrescripcionEjercicio> items) =>
            CompletarAsync(context, items.ToList(), i => i.Ejercicio, i => i.EjercicioId, (i, e) => i.Ejercicio = e);

        public static Task CompletarAsync(GymBroDbContext context, IEnumerable<SesionEjercicio> items) =>
            CompletarAsync(context, items.ToList(), i => i.Ejercicio, i => i.EjercicioId, (i, e) => i.Ejercicio = e);

        private static async Task CompletarAsync<T>(GymBroDbContext context, List<T> items,
            Func<T, Ejercicio?> navegacion, Func<T, Guid> idEjercicio, Action<T, Ejercicio> asignar) where T : class
        {
            var faltantes = items.Where(i => navegacion(i) is null).ToList();
            if (faltantes.Count == 0) return;
            var ids = faltantes.Select(idEjercicio).Distinct().ToList();

            if (faltantes.Any(i => context.Entry(i).State != EntityState.Detached))
            {
                // Entidades con seguimiento: se cargan con seguimiento y EF completa la navegación (asignar una instancia
                // sin seguimiento la marcaría como nueva al guardar).
                await context.Ejercicios.IgnoreQueryFilters().Where(e => ids.Contains(e.Id)).LoadAsync();
                return;
            }

            var ejercicios = await context.Ejercicios.IgnoreQueryFilters().AsNoTracking()
                .Where(e => ids.Contains(e.Id))
                .Select(e => new Ejercicio
                {
                    Id = e.Id, Nombre = e.Nombre, Equipamiento = e.Equipamiento, ZonaObjetivo = e.ZonaObjetivo,
                    MusculoPrincipal = e.MusculoPrincipal, Dificultad = e.Dificultad, TipoMedida = e.TipoMedida,
                    Origen = e.Origen, GimnasioId = e.GimnasioId, FechaBaja = e.FechaBaja
                })
                .ToDictionaryAsync(e => e.Id);
            foreach (var i in faltantes)
                if (ejercicios.TryGetValue(idEjercicio(i), out var e)) asignar(i, e);
        }
    }
}
