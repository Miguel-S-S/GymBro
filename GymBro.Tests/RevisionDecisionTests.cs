using GymBro.Application.Services;
using GymBro.Domain.Enums;
using GymBro.Domain.Exceptions;
using Xunit;

namespace GymBro.Tests
{
    /// <summary>La decisión de revisión solo acepta los nombres Aprobada, Editada o Rechazada (no números ni listas).</summary>
    public class RevisionDecisionTests
    {
        [Theory]
        [InlineData("Aprobada", EstadoPrescripcion.Aprobada)]
        [InlineData("Editada", EstadoPrescripcion.Editada)]
        [InlineData("Rechazada", EstadoPrescripcion.Rechazada)]
        [InlineData("aprobada", EstadoPrescripcion.Aprobada)]
        [InlineData(" RECHAZADA ", EstadoPrescripcion.Rechazada)]
        public void Acepta_los_nombres_de_las_tres_decisiones(string texto, EstadoPrescripcion esperado) =>
            Assert.Equal(esperado, RevisionPrescripcionService.LeerDecision(texto));

        [Theory]
        [InlineData("5")]   // antes se aceptaba como Aprobada
        [InlineData("6")]   // antes, Editada
        [InlineData("7")]   // antes, Rechazada
        [InlineData("1")]
        [InlineData("0")]
        [InlineData("99")]
        [InlineData("-1")]
        [InlineData("Aprobada,Editada")] // Enum.TryParse combinaba valores separados por coma
        [InlineData("Creada")]
        [InlineData("PendienteRevision")]
        [InlineData("Aceptada")]
        [InlineData("Cancelada")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void Rechaza_numeros_listas_y_estados_que_no_son_decisiones(string? texto)
        {
            var ex = Assert.Throws<ReglaNegocioException>(() => RevisionPrescripcionService.LeerDecision(texto));
            Assert.Equal("Decisión inválida. Use Aprobada, Editada o Rechazada.", ex.Message);
        }
    }
}
