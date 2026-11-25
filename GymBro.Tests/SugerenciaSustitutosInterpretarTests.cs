using GymBro.Application.Services;
using Xunit;

namespace GymBro.Tests
{
    /// <summary>Etapa 9b-2: interpretacion estricta de la salida del modelo (forma JSON e ids GUID).</summary>
    public class SugerenciaSustitutosInterpretarTests
    {
        [Fact]
        public void Lee_los_elegidos_con_su_justificacion()
        {
            var id = Guid.NewGuid();
            var r = SugerenciaSustitutosService.Interpretar($"{{\"elegidos\": [{{\"id\": \"{id}\", \"justificacion\": \"sin carga axial\"}}]}}");
            Assert.NotNull(r);
            Assert.Equal((id, "sin carga axial"), Assert.Single(r!));
        }

        [Fact]
        public void Una_lista_vacia_es_valida()
        {
            Assert.Empty(SugerenciaSustitutosService.Interpretar("{\"elegidos\": []}")!);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("prensa de piernas")]
        [InlineData("[]")]
        [InlineData("{\"elegidos\": {}}")]
        [InlineData("{\"elegidos\": [\"prensa\"]}")]
        [InlineData("{\"elegidos\": [{\"id\": 12}]}")]
        [InlineData("{\"elegidos\": [{\"id\": \"no-es-un-guid\"}]}")]
        [InlineData("{\"elegidos\": [{\"nombre\": \"Prensa\"}]}")]
        public void Una_salida_con_otra_forma_devuelve_null(string? texto)
        {
            Assert.Null(SugerenciaSustitutosService.Interpretar(texto));
        }

        [Fact]
        public void La_justificacion_se_recorta_a_300_caracteres()
        {
            var r = SugerenciaSustitutosService.Interpretar($"{{\"elegidos\": [{{\"id\": \"{Guid.NewGuid()}\", \"justificacion\": \"{new string('a', 500)}\"}}]}}");
            Assert.Equal(300, r![0].Justificacion.Length);
        }
    }
}
