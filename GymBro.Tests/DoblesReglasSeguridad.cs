using GymBro.Application.Abstractions;
using GymBro.Application.Services;
using GymBro.Domain.Enums;
using GymBro.Domain.Rules;

namespace GymBro.Tests
{
    /// <summary>Etapa 10a: reglas de seguridad que no hacen nada, o que fallan, para las pruebas de otros servicios.</summary>
    public sealed class ReglasSeguridadDoble : IReglasSeguridadService
    {
        public bool Falla { get; init; }
        public List<Guid> SesionesEvaluadas { get; } = new();
        public List<Guid> RegistrosEvaluados { get; } = new();

        public Task EvaluarSesionAsync(Guid sesionId)
        {
            SesionesEvaluadas.Add(sesionId);
            return Falla ? throw new InvalidOperationException("Fallo simulado de las reglas.") : Task.CompletedTask;
        }

        public Task EvaluarBienestarAsync(Guid registroId)
        {
            RegistrosEvaluados.Add(registroId);
            return Falla ? throw new InvalidOperationException("Fallo simulado de las reglas.") : Task.CompletedTask;
        }

        public IReadOnlyList<ViolacionLimite> ValidarPrescripcion(IReadOnlyCollection<LineaPrescripcion> lineas, NivelExperiencia? nivel) =>
            Array.Empty<ViolacionLimite>();
    }

    public sealed class RegistroEventosDoble : IRegistroEventos
    {
        public List<string> Mensajes { get; } = new();
        public void Advertencia(string mensaje) => Mensajes.Add(mensaje);
        public void Error(string mensaje, Exception excepcion) => Mensajes.Add(mensaje);
    }
}
