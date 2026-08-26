using System;
using System.Collections.Generic;
using System.Text;

namespace GymBro.Application.Services
{
    public interface IIaService
    {
        Task<float[]> GenerarEmbeddingAsync(string texto);
    }
}
