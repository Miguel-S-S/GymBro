using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using GymBro.Application.Sintetico;
using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using GymBro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GymBro.Infrastructure.Sintetico
{
    /// <summary>
    /// Etapa 7e: altas directas, resumen y eliminacion de los datos sinteticos. Las consultas ignoran los filtros globales
    /// a proposito (es una herramienta de desarrollo sin pedido HTTP) y se acotan siempre a gimnasios con el prefijo de
    /// codigo sintetico y a socios con origen Sintetico.
    /// </summary>
    public sealed class AlmacenSintetico : IAlmacenSintetico
    {
        private readonly GymBroDbContext _db;
        public AlmacenSintetico(GymBroDbContext db) { _db = db; }

        public Task<bool> ExisteGimnasioAsync(Guid gimnasioId) =>
            _db.Gimnasios.IgnoreQueryFilters().AnyAsync(g => g.Id == gimnasioId);

        public async Task CrearGimnasioAsync(Gimnasio gimnasio, Usuario dueno)
        {
            if (!gimnasio.Codigo.StartsWith(IAlmacenSintetico.PrefijoCodigo, StringComparison.Ordinal))
                throw new InvalidOperationException("El gimnasio sintetico debe llevar el prefijo de codigo SINT.");
            _db.Gimnasios.Add(gimnasio);
            _db.Usuarios.Add(dueno);
            await _db.SaveChangesAsync();
        }

        public async Task CrearSocioAsync(Socio socio, Usuario cuenta)
        {
            if (socio.OrigenDatos != OrigenDatos.Sintetico)
                throw new InvalidOperationException("El generador solo crea socios con origen Sintetico.");
            _db.Socios.Add(socio);
            _db.Usuarios.Add(cuenta);
            await _db.SaveChangesAsync();
        }

        /// <summary>Clase (multiarticular o aislamiento) y grupo de cada ejercicio del protocolo vigente.</summary>
        public async Task<Dictionary<Guid, (ClaseEjercicio Clase, GrupoMuscularPiernas Grupo)>> ClasesProtocoloAsync(string version) =>
            (await _db.EjerciciosProtocolo.AsNoTracking().Where(e => e.ProtocoloVersion == version)
                .Select(e => new { e.EjercicioId, e.Clase, e.Grupo }).ToListAsync())
            .GroupBy(e => e.EjercicioId).ToDictionary(g => g.Key, g => (g.First().Clase, g.First().Grupo));

        public async Task<ResumenSintetico> ResumirAsync(Guid gimnasioId)
        {
            var c = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            var socios = await _db.Socios.IgnoreQueryFilters().AsNoTracking()
                .Where(s => s.GimnasioId == gimnasioId && s.OrigenDatos == OrigenDatos.Sintetico)
                .OrderBy(s => s.Id).ToListAsync();
            var ids = socios.Select(s => s.Id).ToList();
            foreach (var s in socios)
                sb.Append(c, $"S|{s.Id}|{s.Nombre}|{s.Apellido}|{s.Genero}|{s.FechaNacimiento:yyyy-MM-dd}|{s.Nivel}|{s.Objetivo}|{s.LimitacionRodilla}|{s.LimitacionLumbar}|{s.FechaAlta:O}\n");

            var prescripciones = await _db.PrescripcionesEntrenamiento.IgnoreQueryFilters().AsNoTracking()
                .Where(p => ids.Contains(p.SocioId)).Include(p => p.EjerciciosPrescritos)
                .OrderBy(p => p.SocioId).ThenBy(p => p.NumeroGeneracion).ThenBy(p => p.FechaAlta).ToListAsync();
            foreach (var p in prescripciones)
            {
                sb.Append(c, $"P|{p.SocioId}|{p.NumeroGeneracion}|{p.Estado}|{p.OrigenDatos}|{p.FechaAlta:O}\n");
                foreach (var l in p.EjerciciosPrescritos.OrderBy(l => l.Orden))
                    sb.Append(c, $"PL|{l.Orden}|{l.EjercicioId}|{l.SeriesObjetivo}|{l.RepeticionesMinimas}|{l.RepeticionesMaximas}|{l.RIRObjetivo}|{l.DescansoObjetivoSegundos}|{l.CargaSugeridaKg}|{l.Rol}|{l.Grupo}\n");
            }

            var sesiones = await _db.Sesiones.IgnoreQueryFilters().AsNoTracking()
                .Where(s => ids.Contains(s.SocioId))
                .Include(s => s.EjerciciosEjecutados).ThenInclude(e => e.SeriesRealizadas)
                .OrderBy(s => s.SocioId).ThenBy(s => s.Inicio).ToListAsync();
            var series = 0;
            foreach (var s in sesiones)
            {
                sb.Append(c, $"SE|{s.SocioId}|{s.Inicio:O}|{s.Fin:O}\n");
                foreach (var e in s.EjerciciosEjecutados.OrderBy(e => e.Orden))
                {
                    sb.Append(c, $"SJ|{e.Orden}|{e.EjercicioId}\n");
                    foreach (var r in e.SeriesRealizadas.OrderBy(r => r.OrdenSerie))
                    {
                        series++;
                        sb.Append(c, $"SR|{r.OrdenSerie}|{r.PesoLevantadoKg}|{r.Repeticiones}|{r.RIR}|{r.EsCalentamiento}|{r.TiempoDescansoSegundos}\n");
                    }
                }
            }

            var bienestar = await _db.RegistrosBienestarDiario.IgnoreQueryFilters().AsNoTracking()
                .Where(r => ids.Contains(r.SocioId)).OrderBy(r => r.SocioId).ThenBy(r => r.Fecha).ToListAsync();
            foreach (var r in bienestar)
                sb.Append(c, $"B|{r.SocioId}|{r.Fecha:yyyy-MM-dd}|{r.HorasSueno}|{r.Estres}|{r.Fatiga}|{r.PesoKg}|{r.NutricionCategoria}|{r.IngestaAguaLitros}|{r.FrecuenciaCardiacaReposo}\n");

            var asistencias = await _db.Asistencias.IgnoreQueryFilters().AsNoTracking()
                .Where(a => a.GimnasioId == gimnasioId).OrderBy(a => a.SocioId).ThenBy(a => a.Fecha).ToListAsync();
            foreach (var a in asistencias)
                sb.Append(c, $"A|{a.SocioId}|{a.Fecha:yyyy-MM-dd}|{a.Instante:O}|{a.Metodo}|{a.OrigenDatos}\n");

            var cuotas = await _db.CuotasSocio.IgnoreQueryFilters().AsNoTracking()
                .Where(q => q.GimnasioId == gimnasioId).OrderBy(q => q.SocioId).ThenBy(q => q.Anio).ThenBy(q => q.Mes).ToListAsync();
            foreach (var q in cuotas)
                sb.Append(c, $"C|{q.SocioId}|{q.Anio}-{q.Mes}|{q.Monto}|{q.Vencimiento:yyyy-MM-dd}|{q.FechaPago:yyyy-MM-dd}|{q.Estado}|{q.OrigenDatos}\n");

            var alertas = await _db.AlertasSeguridad.IgnoreQueryFilters().AsNoTracking()
                .Where(a => ids.Contains(a.SocioId)).ToListAsync();
            foreach (var a in alertas.OrderBy(a => a.SocioId).ThenBy(a => a.ReglaCodigo).ThenBy(a => a.Mensaje, StringComparer.Ordinal)
                         .ThenBy(a => a.Estado).ThenBy(a => a.FechaBaja is null))
                sb.Append(c, $"AL|{a.SocioId}|{a.ReglaCodigo}|{a.Tipo}|{a.Severidad}|{a.Estado}|{a.Mensaje}|{a.FechaBaja is null}\n");

            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()))).ToLowerInvariant();
            return new ResumenSintetico(hash, socios.Count, sesiones.Count, series, prescripciones.Count, bienestar.Count,
                asistencias.Count, cuotas.Count, alertas.Count);
        }

        public async Task<EliminacionSintetica> EliminarAsync()
        {
            await using var tx = await _db.Database.BeginTransactionAsync();
            var gimnasios = await _db.Gimnasios.IgnoreQueryFilters()
                .Where(g => g.Codigo.StartsWith(IAlmacenSintetico.PrefijoCodigo)).Select(g => g.Id).ToListAsync();
            var socios = await _db.Socios.IgnoreQueryFilters()
                .Where(s => s.GimnasioId != null && gimnasios.Contains(s.GimnasioId.Value) && s.OrigenDatos == OrigenDatos.Sintetico)
                .Select(s => s.Id).ToListAsync();

            var prescripciones = _db.PrescripcionesEntrenamiento.IgnoreQueryFilters().Where(p => socios.Contains(p.SocioId)).Select(p => p.Id);
            var sesiones = _db.Sesiones.IgnoreQueryFilters().Where(s => socios.Contains(s.SocioId)).Select(s => s.Id);
            var sesionEjercicios = _db.SesionesEjercicios.IgnoreQueryFilters().Where(e => sesiones.Contains(e.SesionId)).Select(e => e.Id);

            await _db.AlertasSeguridad.IgnoreQueryFilters().Where(a => socios.Contains(a.SocioId)).ExecuteDeleteAsync();
            var asistencias = await _db.Asistencias.IgnoreQueryFilters().Where(a => socios.Contains(a.SocioId)).ExecuteDeleteAsync();
            var cuotas = await _db.CuotasSocio.IgnoreQueryFilters().Where(c => socios.Contains(c.SocioId)).ExecuteDeleteAsync();
            await _db.SeriesRealizadas.IgnoreQueryFilters().Where(r => sesionEjercicios.Contains(r.SesionEjercicioId)).ExecuteDeleteAsync();
            await _db.SesionesEjercicios.IgnoreQueryFilters().Where(e => sesiones.Contains(e.SesionId)).ExecuteDeleteAsync();
            var nSesiones = await _db.Sesiones.IgnoreQueryFilters().Where(s => socios.Contains(s.SocioId)).ExecuteDeleteAsync();
            await _db.RevisionesPrescripcion.Where(r => prescripciones.Contains(r.PrescripcionEntrenamientoId)).ExecuteDeleteAsync();
            await _db.PrescripcionesEjercicios.IgnoreQueryFilters().Where(l => prescripciones.Contains(l.PrescripcionEntrenamientoId)).ExecuteDeleteAsync();
            await _db.PrescripcionesEntrenamiento.IgnoreQueryFilters().Where(p => socios.Contains(p.SocioId)).ExecuteDeleteAsync();
            await _db.RegistrosBienestarDiario.IgnoreQueryFilters().Where(r => socios.Contains(r.SocioId)).ExecuteDeleteAsync();
            await _db.MedicionesFisicas.IgnoreQueryFilters().Where(m => socios.Contains(m.SocioId)).ExecuteDeleteAsync();
            await _db.Asignaciones.IgnoreQueryFilters().Where(a => socios.Contains(a.SocioId)).ExecuteDeleteAsync();
            await _db.SolicitudesVinculacion.IgnoreQueryFilters().Where(s => socios.Contains(s.SocioId)).ExecuteDeleteAsync();
            await _db.Invitaciones.IgnoreQueryFilters().Where(i => socios.Contains(i.SocioId)).ExecuteDeleteAsync();
            await EliminarExtraPorSocioAsync(socios);
            var usuariosSocio = await _db.Usuarios.IgnoreQueryFilters().Where(u => u.SocioId != null && socios.Contains(u.SocioId.Value)).ExecuteDeleteAsync();
            var nSocios = await _db.Socios.IgnoreQueryFilters().Where(s => socios.Contains(s.Id)).ExecuteDeleteAsync();

            // El gimnasio solo se elimina si ya no le quedan socios (de ningun origen).
            var vacios = await _db.Gimnasios.IgnoreQueryFilters()
                .Where(g => gimnasios.Contains(g.Id) && !_db.Socios.IgnoreQueryFilters().Any(s => s.GimnasioId == g.Id))
                .Select(g => g.Id).ToListAsync();
            await _db.Asistencias.IgnoreQueryFilters().Where(a => vacios.Contains(a.GimnasioId)).ExecuteDeleteAsync();
            await _db.CuotasSocio.IgnoreQueryFilters().Where(c => vacios.Contains(c.GimnasioId)).ExecuteDeleteAsync();
            await _db.Asignaciones.IgnoreQueryFilters().Where(a => vacios.Contains(a.GimnasioId)).ExecuteDeleteAsync();
            await _db.EquipamientosGimnasio.IgnoreQueryFilters().Where(e => vacios.Contains(e.GimnasioId)).ExecuteDeleteAsync();
            var usuariosPersonal = await _db.Usuarios.IgnoreQueryFilters().Where(u => u.GimnasioId != null && vacios.Contains(u.GimnasioId.Value)).ExecuteDeleteAsync();
            var nGimnasios = await _db.Gimnasios.IgnoreQueryFilters().Where(g => vacios.Contains(g.Id)).ExecuteDeleteAsync();
            await tx.CommitAsync();
            return new EliminacionSintetica(nGimnasios, nSocios, nSesiones, asistencias, cuotas, usuariosSocio + usuariosPersonal);
        }

        /// <summary>Tablas agregadas por etapas posteriores que referencian al socio (por ejemplo, prediccion_abandono).</summary>
        private Task EliminarExtraPorSocioAsync(List<Guid> socios) => Task.CompletedTask;
    }
}
