using System.Net.Http.Json;
using GymBro.Application.Services;
using GymBro.Domain.Exceptions;

namespace GymBro.Infrastructure.ExternalServices
{
    /// <summary>
    /// Etapa 9b-2: cliente de GymBro.IA /sugerir-sustitutos/. Devuelve el texto del modelo sin interpretarlo: la
    /// verificacion (forma JSON y pertenencia de los ids al conjunto recuperado) la hace SugerenciaSustitutosService.
    /// </summary>
    public class GeneradorSustitutosIa : IGeneradorSustitutos
    {
        private readonly HttpClient _http;
        public GeneradorSustitutosIa(HttpClient http) { _http = http; }

        public async Task<RespuestaGenerador> GenerarAsync(PedidoSustitutos pedido)
        {
            static object Ref(Application.DTOs.EjercicioBuscadoDto e) => new
            {
                id = e.Id.ToString(), nombre = e.Nombre, zona = e.ZonaObjetivo, musculo = e.MusculoPrincipal, equipamiento = e.Equipamiento,
            };
            try
            {
                var r = await _http.PostAsJsonAsync("/sugerir-sustitutos/", new
                {
                    ejercicio = Ref(pedido.Ejercicio),
                    motivo = pedido.Motivo,
                    candidatos = pedido.Candidatos.Select(Ref).ToList(),
                    maximo = pedido.Maximo,
                });
                r.EnsureSuccessStatusCode();
                var cuerpo = await r.Content.ReadFromJsonAsync<CuerpoRespuesta>()
                    ?? throw new ServicioExternoNoDisponibleException("El servicio de IA devolvió una respuesta vacía.");
                return new RespuestaGenerador(cuerpo.Modelo ?? string.Empty, cuerpo.Respuesta ?? string.Empty);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
            {
                throw new ServicioExternoNoDisponibleException(
                    "El modelo de lenguaje local no está disponible en este momento. Intente más tarde.", ex);
            }
        }

        private sealed class CuerpoRespuesta
        {
            public string? Modelo { get; set; }
            public string? Respuesta { get; set; }
        }
    }
}
