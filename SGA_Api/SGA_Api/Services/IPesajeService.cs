using SGA_Api.Models.Pesaje;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SGA_Api.Services
{
    public interface IPesajeService
    {
        Task<PesajeResponseDto?> GetPesajeAsync(int ejercicio, string serie, int numero);
    }
}

