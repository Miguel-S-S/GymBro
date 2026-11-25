using System.Globalization;
using GymBro.Application.Sintetico;
using GymBro.Domain.Security;
using Microsoft.Extensions.DependencyInjection;

namespace GymBro.Infrastructure.Sintetico
{
    /// <summary>
    /// Etapa 7e: modo generador de la API (herramienta de desarrollo). Se activa solo con una opcion explicita de linea de
    /// comandos y solo en el entorno Development (lo verifica Program.cs antes de registrar nada). En ese modo la API no
    /// atiende pedidos HTTP: genera o elimina los datos sinteticos y termina.
    /// </summary>
    public static class ModoSintetico
    {
        public const string OpcionGenerar = "--generar-sintetico";
        public const string OpcionEliminar = "--eliminar-sintetico";

        public static bool Solicitado(string[] args) => args.Contains(OpcionGenerar) || args.Contains(OpcionEliminar);

        /// <summary>
        /// Reemplaza la identidad HTTP y el reloj del sistema por los simulados. Se llama despues de los registros normales
        /// (el ultimo registro gana) y nunca en el modo normal de la API.
        /// </summary>
        public static IServiceCollection AgregarModoSintetico(this IServiceCollection services)
        {
            services.AddSingleton<ContextoSintetico>();
            services.AddSingleton<RelojSintetico>();
            services.AddSingleton<TimeProvider>(sp => sp.GetRequiredService<RelojSintetico>());
            services.AddScoped<IUsuarioActual, UsuarioActualSintetico>();
            services.AddScoped<AlmacenSintetico>();
            services.AddScoped<IAlmacenSintetico>(sp => sp.GetRequiredService<AlmacenSintetico>());
            services.AddSingleton<GeneradorSintetico>();
            return services;
        }

        /// <summary>Lee --semilla, --socios, --semanas e --inicio (yyyy-MM-dd, un lunes). Lo demas queda con los valores por defecto.</summary>
        public static ParametrosSinteticos LeerParametros(string[] args)
        {
            var p = new ParametrosSinteticos();
            string? Valor(string nombre)
            {
                var i = Array.IndexOf(args, nombre);
                return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
            }
            if (Valor("--semilla") is { } semilla) p.Semilla = int.Parse(semilla, CultureInfo.InvariantCulture);
            if (Valor("--socios") is { } socios) p.Socios = int.Parse(socios, CultureInfo.InvariantCulture);
            if (Valor("--semanas") is { } semanas) p.Semanas = int.Parse(semanas, CultureInfo.InvariantCulture);
            if (Valor("--inicio") is { } inicio) p.Inicio = DateOnly.ParseExact(inicio, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            p.Validar();
            return p;
        }

        /// <summary>Ejecuta la opcion pedida. Devuelve el codigo de salida del proceso.</summary>
        public static async Task<int> EjecutarAsync(IServiceProvider services, string[] args, Action<string> log)
        {
            var generador = services.GetRequiredService<GeneradorSintetico>();
            if (args.Contains(OpcionEliminar))
            {
                var e = await generador.EliminarAsync();
                log($"Eliminados: {e.Gimnasios} gimnasios, {e.Socios} socios, {e.Sesiones} sesiones, {e.Asistencias} asistencias, " +
                    $"{e.Cuotas} cuotas y {e.Usuarios} cuentas sinteticas. La auditoria no se borra.");
            }
            if (args.Contains(OpcionGenerar))
            {
                var p = LeerParametros(args);
                log($"Generando datos sinteticos: semilla {p.Semilla}, {p.Socios} socios, {p.Semanas} semanas desde {p.Inicio:yyyy-MM-dd}.");
                var r = await generador.GenerarAsync(p, log);
                log($"Gimnasio {r.GimnasioCodigo} ({r.GimnasioId}). Resumen SHA-256: {r.Resumen.Sha256}");
            }
            return 0;
        }
    }
}
