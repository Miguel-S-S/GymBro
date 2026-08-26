using GymBro.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Text;

namespace GymBro.Application.Services
{
    public interface IMedicionService
    {
        Task RegistrarMedicionAsync(MedicionCreateDto dto);
    }
}
