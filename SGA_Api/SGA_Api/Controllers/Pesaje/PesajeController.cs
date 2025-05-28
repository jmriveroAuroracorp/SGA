using Microsoft.AspNetCore.Mvc;
using SGA_Api.Models.Pesaje;
using SGA_Api.Services; 

namespace SGA_Api.Controllers.Pesaje
{
    [ApiController]
    [Route("api/[controller]")]
    public class PesajeController : ControllerBase
    {
        private readonly IPesajeService _pesajeLogic;

        public PesajeController(IPesajeService pesajeLogic)
        {
            _pesajeLogic = pesajeLogic;
        }

        [HttpGet("{ejercicio}/{serie}/{numero}")]
        public async Task<ActionResult<PesajeResponseDto>> GetPesaje(int ejercicio, string serie, int numero)
        {
            var resultado = await _pesajeLogic.GetPesajeAsync(ejercicio, serie, numero);

            if (resultado == null)
                return NotFound("No se encontraron datos para esa orden.");

            return Ok(resultado);
        }
    }
}
