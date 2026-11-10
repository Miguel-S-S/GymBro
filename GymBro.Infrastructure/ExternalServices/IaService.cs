using System.ComponentModel;
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

        public async Task<float[]> GenerarEmbeddingAsync(string texto)
        {
            var response = await _httpClient.PostAsync($"/generar-embedding/?texto={Uri.EscapeDataString(texto)}", null);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<IaResponse>();
            return result?.Embedding ?? Array.Empty<float>();
        }
        //Pass.Labora_01
        // Clase privada solo para atrapar la respuesta de Python
        private class IaResponse
        {
            public float[] Embedding { get; set; } = Array.Empty<float>();
        }
    }
}