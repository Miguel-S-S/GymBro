using System.Collections.Generic;

namespace GymBro.Application.DTOs
{
    public class PaginadoDto<T>
    {
        public IReadOnlyList<T> Items { get; set; } = new List<T>();
        public int Total { get; set; }
        public int Pagina { get; set; }
        public int TamanioPagina { get; set; }
    }
}
