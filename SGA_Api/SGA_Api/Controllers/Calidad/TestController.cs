using Microsoft.AspNetCore.Mvc;
using SGA_Api.Models.Calidad;
using SGA_Api.Services;

namespace SGA_Api.Controllers.Calidad
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        private readonly ICalidadService _calidadService;

        public TestController(ICalidadService calidadService)
        {
            _calidadService = calidadService;
        }

        /// <summary>
        /// Endpoint de prueba SIN autenticación
        /// </summary>
        [HttpGet("stock")]
        public async Task<IActionResult> TestStock(
            [FromQuery] short codigoEmpresa,
            [FromQuery] string codigoArticulo,
            [FromQuery] string partida,
            [FromQuery] string? codigoAlmacen = null,
            [FromQuery] string? codigoUbicacion = null)
        {
            try
            {
                // Validaciones básicas
                if (codigoEmpresa <= 0)
                    return BadRequest("Código de empresa es obligatorio");

                if (string.IsNullOrWhiteSpace(codigoArticulo))
                    return BadRequest("Código de artículo es obligatorio");

                if (string.IsNullOrWhiteSpace(partida))
                    return BadRequest("Lote/partida es obligatorio");

                // Buscar stock directamente
                var stockData = await _calidadService.BuscarStockPorArticuloYLoteAsync(
                    codigoEmpresa, codigoArticulo, partida, codigoAlmacen, codigoUbicacion);

                return Ok(new { 
                    mensaje = "Test exitoso", 
                    resultados = stockData.Count,
                    data = stockData 
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
