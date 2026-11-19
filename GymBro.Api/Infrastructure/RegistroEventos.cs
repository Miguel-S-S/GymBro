using GymBro.Application.Abstractions;

namespace GymBro.Api.Infrastructure
{
    /// <summary>Etapa 10a: IRegistroEventos sobre ILogger. Los mensajes no deben llevar datos de salud.</summary>
    public class RegistroEventos : IRegistroEventos
    {
        private readonly ILogger<RegistroEventos> _logger;
        public RegistroEventos(ILogger<RegistroEventos> logger) { _logger = logger; }

        public void Advertencia(string mensaje) => _logger.LogWarning("{Mensaje}", mensaje);

        public void Error(string mensaje, Exception excepcion) => _logger.LogError(excepcion, "{Mensaje}", mensaje);
    }
}
