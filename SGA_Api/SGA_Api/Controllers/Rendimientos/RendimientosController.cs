using Microsoft.AspNetCore.Mvc;
using SGA_Api.Models.Rendimientos;
using SGA_Api.Services;

namespace SGA_Api.Controllers.Rendimientos
{
    [ApiController]
    [Route("api/[controller]")]
    public class RendimientosController : ControllerBase
    {
        private readonly RendimientosService _rendimientosService;
        private readonly ILogger<RendimientosController> _logger;

        public RendimientosController(
            RendimientosService rendimientosService,
            ILogger<RendimientosController> logger)
        {
            _rendimientosService = rendimientosService;
            _logger = logger;
        }

        [HttpGet("operarios")]
        public async Task<IActionResult> ObtenerRendimientoOperarios([FromQuery] FiltroRendimientosDto filtros)
        {
            try
            {
                _logger.LogInformation("Obteniendo rendimiento de operarios");
                var resultados = await _rendimientosService.ObtenerRendimientoOperariosAsync(filtros);
                return Ok(resultados);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo rendimiento de operarios");
                return StatusCode(500, new { message = "Error al obtener rendimiento de operarios", error = ex.Message });
            }
        }

        [HttpGet("procesos")]
        public async Task<IActionResult> ObtenerRendimientoProcesos([FromQuery] FiltroRendimientosDto filtros)
        {
            try
            {
                _logger.LogInformation("Obteniendo rendimiento de procesos");
                var resultados = await _rendimientosService.ObtenerRendimientoProcesosAsync(filtros);
                return Ok(resultados);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo rendimiento de procesos");
                return StatusCode(500, new { message = "Error al obtener rendimiento de procesos", error = ex.Message });
            }
        }

        [HttpGet("comparativa")]
        public async Task<IActionResult> ObtenerComparativa(
            [FromQuery] FiltroRendimientosDto filtros,
            [FromQuery] string tipoComparativa = "OPERARIOS")
        {
            try
            {
                _logger.LogInformation("Obteniendo comparativa de tipo: {Tipo}", tipoComparativa);
                var resultado = await _rendimientosService.ObtenerComparativaAsync(filtros, tipoComparativa);
                return Ok(resultado);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo comparativa");
                return StatusCode(500, new { message = "Error al obtener comparativa", error = ex.Message });
            }
        }

        [HttpGet("tendencias")]
        public async Task<IActionResult> ObtenerTendencias(
            [FromQuery] FiltroRendimientosDto filtros,
            [FromQuery] string tipoMetrica = "PRODUCTIVIDAD")
        {
            try
            {
                _logger.LogInformation("Obteniendo tendencias de tipo: {Tipo}", tipoMetrica);
                var resultados = await _rendimientosService.ObtenerTendenciasAsync(filtros, tipoMetrica);
                return Ok(resultados);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo tendencias");
                return StatusCode(500, new { message = "Error al obtener tendencias", error = ex.Message });
            }
        }

        [HttpGet("volumen")]
        public async Task<IActionResult> ObtenerVolumenMovido([FromQuery] FiltroRendimientosDto filtros)
        {
            try
            {
                _logger.LogInformation("Obteniendo volumen movido");
                var resultado = await _rendimientosService.ObtenerVolumenMovidoAsync(filtros);
                return Ok(resultado);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo volumen movido");
                return StatusCode(500, new { message = "Error al obtener volumen movido", error = ex.Message });
            }
        }

        [HttpGet("distribucion")]
        public async Task<IActionResult> ObtenerDistribucion([FromQuery] FiltroRendimientosDto filtros)
        {
            try
            {
                _logger.LogInformation("Obteniendo distribución");
                var resultado = await _rendimientosService.ObtenerDistribucionAsync(filtros);
                return Ok(resultado);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo distribución");
                return StatusCode(500, new { message = "Error al obtener distribución", error = ex.Message });
            }
        }

        [HttpGet("articulos")]
        public async Task<IActionResult> ObtenerRendimientoArticulos([FromQuery] FiltroRendimientosDto filtros)
        {
            try
            {
                _logger.LogInformation("Obteniendo rendimiento de artículos");
                var resultados = await _rendimientosService.ObtenerRendimientoArticulosAsync(filtros);
                return Ok(resultados);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo rendimiento de artículos");
                return StatusCode(500, new { message = "Error al obtener rendimiento de artículos", error = ex.Message });
            }
        }

        [HttpPost("exportar")]
        public async Task<IActionResult> ExportarInforme([FromBody] ExportarInformeDto dto)
        {
            try
            {
                _logger.LogInformation("Solicitando exportación de informe: {Tipo}", dto.TipoInforme);
                // Por ahora retornamos un mensaje, la implementación de exportación se hará después
                return Ok(new { message = "Exportación de informes pendiente de implementar" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exportando informe");
                return StatusCode(500, new { message = "Error al exportar informe", error = ex.Message });
            }
        }
    }
}

