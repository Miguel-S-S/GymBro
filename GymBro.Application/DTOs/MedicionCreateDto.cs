using System;
using System.Collections.Generic;
using System.Text;

namespace GymBro.Application.DTOs
{
    public class MedicionCreateDto
    {
        public Guid SocioId { get; set; }
        public decimal PesoKg { get; set; }
        public decimal AlturaCm { get; set; }
      
    }
}
