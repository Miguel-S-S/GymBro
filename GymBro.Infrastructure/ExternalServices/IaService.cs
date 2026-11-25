using System.Net.Http.Json;
using GymBro.Application.Services;

namespace GymBro.Infrastructure.ExternalServices
{
    public class IaService : IIaService
    {
        private readonly HttpClient _httpClient;

        public IaService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <summary>
        /// Etapa 9b-1: el texto viaja en el cuerpo JSON ({"texto": "..."}) y no en la URL. GymBro.IA/main.py acepta las dos
        /// formas para no romper a otros llamadores.
        /// </summary>
        public async Task<float[]> GenerarEmbeddingAsync(string texto)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/generar-embedding/", new { texto });
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<IaResponse>();
                return result?.Embedding ?? Array.Empty<float>();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
            {
                throw new GymBro.Domain.Exceptions.ServicioExternoNoDisponibleException(
                    "El servicio de IA no está disponible en este momento. Intente más tarde.", ex);
            }
        }

        // Clase privada solo para atrapar la respuesta de Python
        private class IaResponse
        {
            public float[] Embedding { get; set; } = Array.Empty<float>();
        }
    }
}
