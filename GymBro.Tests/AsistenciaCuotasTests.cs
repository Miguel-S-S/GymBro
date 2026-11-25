using GymBro.Application.Services;
using GymBro.Domain.Entities;
using GymBro.Domain.Enums;
using Xunit;

namespace GymBro.Tests
{
    /// <summary>Etapa 11: token del QR de asistencia y morosidad derivada de la cuota. Datos sinteticos.</summary>
    public class AsistenciaCuotasTests
    {
        private const string Clave = "clave-de-prueba-unitaria-qr-asistencia-0001";
        private static readonly DateTimeOffset Ahora = new(2026, 11, 25, 15, 0, 0, TimeSpan.Zero);

        [Fact]
        public void Un_token_vigente_devuelve_el_socio()
        {
            var socio = Guid.NewGuid();
            var token = TokenAsistencia.Generar(socio, Ahora.AddMinutes(2), Clave);
            Assert.Equal(TokenAsistencia.Resultado.Valido, TokenAsistencia.Validar(token, Ahora, Clave, out var leido));
            Assert.Equal(socio, leido);
        }

        [Fact]
        public void El_token_es_Base32_corto_y_se_acepta_con_guiones_y_minusculas()
        {
            var token = TokenAsistencia.Generar(Guid.NewGuid(), Ahora.AddMinutes(2), Clave);
            Assert.Equal(60, token.Length);
            Assert.Matches("^[A-Z2-7]+$", token);
            var tipeado = string.Join("-", token.ToLowerInvariant().Chunk(4).Select(c => new string(c)));
            Assert.Equal(TokenAsistencia.Resultado.Valido, TokenAsistencia.Validar(tipeado, Ahora, Clave, out _));
        }

        [Fact]
        public void Vence_un_segundo_despues_del_limite_y_no_antes()
        {
            var vence = Ahora.AddSeconds(120);
            var token = TokenAsistencia.Generar(Guid.NewGuid(), vence, Clave);
            Assert.Equal(TokenAsistencia.Resultado.Valido, TokenAsistencia.Validar(token, vence, Clave, out _));
            Assert.Equal(TokenAsistencia.Resultado.Vencido, TokenAsistencia.Validar(token, vence.AddSeconds(1), Clave, out _));
        }

        [Fact]
        public void Otra_clave_o_un_caracter_alterado_invalidan_la_firma()
        {
            var token = TokenAsistencia.Generar(Guid.NewGuid(), Ahora.AddMinutes(2), Clave);
            Assert.Equal(TokenAsistencia.Resultado.FirmaInvalida, TokenAsistencia.Validar(token, Ahora, Clave + "x", out var socio));
            Assert.Equal(Guid.Empty, socio);
            // Se altera un caracter del medio (parte del id del socio): la firma ya no coincide.
            var alterado = token[..10] + (token[10] == 'A' ? 'B' : 'A') + token[11..];
            Assert.Equal(TokenAsistencia.Resultado.FirmaInvalida, TokenAsistencia.Validar(alterado, Ahora, Clave, out _));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("1890")]
        [InlineData("ABCDEFGH")]
        public void Un_texto_que_no_es_token_es_malformado(string? texto)
        {
            Assert.Equal(TokenAsistencia.Resultado.Malformado, TokenAsistencia.Validar(texto, Ahora, Clave, out _));
        }

        [Fact]
        public void Sin_clave_de_al_menos_32_bytes_el_QR_queda_deshabilitado()
        {
            Assert.False(new AsistenciaConfig().QrHabilitado);
            Assert.False(new AsistenciaConfig { ClaveQr = new string('x', 31) }.QrHabilitado);
            Assert.True(new AsistenciaConfig { ClaveQr = new string('x', 32) }.QrHabilitado);
            Assert.Equal(120, new AsistenciaConfig().DuracionTokenSegundos);
        }

        [Theory]
        [InlineData(EstadoCuota.Pendiente, "2026-11-24", true)]   // vencio ayer
        [InlineData(EstadoCuota.Pendiente, "2026-11-25", false)]  // vence hoy: todavia no
        [InlineData(EstadoCuota.Pendiente, "2026-12-10", false)]
        [InlineData(EstadoCuota.Pagada, "2026-11-01", false)]     // pagada: nunca vencida
        public void La_morosidad_se_deriva_del_estado_y_el_vencimiento(EstadoCuota estado, string vencimiento, bool vencida)
        {
            var cuota = new CuotaSocio
            {
                Estado = estado, Vencimiento = DateOnly.Parse(vencimiento),
                FechaPago = estado == EstadoCuota.Pagada ? new DateOnly(2026, 11, 1) : null,
            };
            Assert.Equal(vencida, cuota.EstaVencida(new DateOnly(2026, 11, 25)));
        }
    }
}
