using Microsoft.AspNetCore.Mvc;
using SGA_Api.Data;
using SGA_Api.Helpers;
using SGA_Api.Models.Calidad;
using SGA_Api.Services;

namespace SGA_Api.Controllers.Calidad
{
    [ApiController]
    [Route("api/[controller]")]
    public class CalidadController : ControllerBase
    {
        private readonly ICalidadService _calidadService;
        private readonly AuroraSgaDbContext _auroraSgaContext;
        private readonly ILogger<CalidadController> _logger;

        public CalidadController(
            ICalidadService calidadService,
            AuroraSgaDbContext auroraSgaContext,
            ILogger<CalidadController> logger)
        {
            _calidadService = calidadService;
            _auroraSgaContext = auroraSgaContext;
            _logger = logger;
        }

        /// <summary>
        /// Busca stock por artículo y lote para bloqueo de calidad
        /// </summary>
        /// <param name="codigoEmpresa">Código de empresa (obligatorio)</param>
        /// <param name="codigoArticulo">Código de artículo (obligatorio)</param>
        /// <param name="partida">Lote/partida (obligatorio)</param>
        /// <param name="codigoAlmacen">Código de almacén (opcional)</param>
        /// <param name="codigoUbicacion">Código de ubicación (opcional)</param>
        /// <returns>Lista de stock encontrado</returns>
        [HttpGet("buscar-stock")]
        [ProducesResponseType(typeof(List<StockCalidadDto>), 200)]
        [ProducesResponseType(typeof(ProblemDetails), 400)]
        [ProducesResponseType(typeof(ProblemDetails), 500)]
        public async Task<IActionResult> BuscarStockCalidad(
            [FromQuery] short codigoEmpresa,    // OBLIGATORIO - empresa del usuario
            [FromQuery] string codigoArticulo,  // OBLIGATORIO
            [FromQuery] string partida,        // OBLIGATORIO
            [FromQuery] string? codigoAlmacen = null,      // Opcional - para filtrar por almacén
            [FromQuery] string? codigoUbicacion = null)     // Opcional - para filtrar por ubicación
        {
            try
            {
                // 1. Validar parámetros obligatorios
                if (codigoEmpresa <= 0)
                    return BadRequest(new ProblemDetails
                    {
                        Title = "Parámetro inválido",
                        Detail = "Código de empresa es obligatorio y debe ser mayor a 0",
                        Status = 400
                    });
                
                if (string.IsNullOrWhiteSpace(codigoArticulo))
                    return BadRequest(new ProblemDetails
                    {
                        Title = "Parámetro inválido",
                        Detail = "Código de artículo es obligatorio",
                        Status = 400
                    });
                
                if (string.IsNullOrWhiteSpace(partida))
                    return BadRequest(new ProblemDetails
                    {
                        Title = "Parámetro inválido",
                        Detail = "Lote/partida es obligatorio",
                        Status = 400
                    });

                // 2. Obtener usuario desde token
                var usuarioId = await UsuarioHelper.ObtenerUsuarioDesdeTokenAsync(HttpContext, _auroraSgaContext);
                if (!usuarioId.HasValue)
                {
                    return Unauthorized(new ProblemDetails
                    {
                        Title = "No autorizado",
                        Detail = "Token de sesión inválido",
                        Status = 401
                    });
                }

                // 3. Verificar permiso 16 (Calidad)
                if (!await _calidadService.VerificarPermisoCalidadAsync(usuarioId.Value))
                {
                    return Unauthorized(new ProblemDetails
                    {
                        Title = "Sin permisos",
                        Detail = "No tiene permisos para acceder a Calidad",
                        Status = 403
                    });
                }

                // 4. Verificar que el usuario tiene acceso a la empresa
                if (!await _calidadService.VerificarAccesoEmpresaAsync(usuarioId.Value, codigoEmpresa))
                {
                    return Unauthorized(new ProblemDetails
                    {
                        Title = "Sin acceso",
                        Detail = "No tiene acceso a esta empresa",
                        Status = 403
                    });
                }

                // 5. Buscar stock con filtros obligatorios
                var stockData = await _calidadService.BuscarStockPorArticuloYLoteAsync(
                    codigoEmpresa, codigoArticulo, partida, codigoAlmacen, codigoUbicacion);

                _logger.LogInformation("Búsqueda de stock completada para usuario {UsuarioId}, empresa {CodigoEmpresa}, artículo {CodigoArticulo}, partida {Partida}. Resultados: {Count}",
                    usuarioId.Value, codigoEmpresa, codigoArticulo, partida, stockData.Count);

                return Ok(stockData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en búsqueda de stock para empresa {CodigoEmpresa}, artículo {CodigoArticulo}, partida {Partida}",
                    codigoEmpresa, codigoArticulo, partida);

                return StatusCode(500, new ProblemDetails
                {
                    Title = "Error interno del servidor",
                    Detail = "Ocurrió un error al buscar el stock",
                    Status = 500
                });
            }
        }

        /// <summary>
        /// Bloquea stock específico
        /// </summary>
        /// <param name="dto">Datos del bloqueo</param>
        /// <returns>Resultado del bloqueo</returns>
        [HttpPost("bloquear-stock")]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(typeof(ProblemDetails), 400)]
        [ProducesResponseType(typeof(ProblemDetails), 403)]
        [ProducesResponseType(typeof(ProblemDetails), 500)]
        public async Task<IActionResult> BloquearStock([FromBody] BloquearStockDto dto)
        {
            try
            {
                // 1. Obtener usuario desde token
                var usuarioId = await UsuarioHelper.ObtenerUsuarioDesdeTokenAsync(HttpContext, _auroraSgaContext);
                if (!usuarioId.HasValue)
                {
                    return Unauthorized(new ProblemDetails
                    {
                        Title = "No autorizado",
                        Detail = "Token de sesión inválido",
                        Status = 401
                    });
                }

                // 2. Verificar permiso 16 (Calidad)
                if (!await _calidadService.VerificarPermisoCalidadAsync(usuarioId.Value))
                {
                    return Unauthorized(new ProblemDetails
                    {
                        Title = "Sin permisos",
                        Detail = "No tiene permisos para acceder a Calidad",
                        Status = 403
                    });
                }

                // 3. Verificar que el usuario tiene acceso a la empresa
                if (!await _calidadService.VerificarAccesoEmpresaAsync(usuarioId.Value, dto.CodigoEmpresa))
                {
                    return Unauthorized(new ProblemDetails
                    {
                        Title = "Sin acceso",
                        Detail = "No tiene acceso a esta empresa",
                        Status = 403
                    });
                }

                // 4. Asignar usuario al DTO
                dto.UsuarioId = usuarioId.Value;

                // 5. Ejecutar bloqueo
                var resultado = await _calidadService.BloquearStockAsync(dto);

                _logger.LogInformation("Bloqueo de stock ejecutado para usuario {UsuarioId}, empresa {CodigoEmpresa}, artículo {CodigoArticulo}, partida {Partida}",
                    usuarioId.Value, dto.CodigoEmpresa, dto.CodigoArticulo, dto.LotePartida);

                return Ok(resultado);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en bloqueo de stock para empresa {CodigoEmpresa}, artículo {CodigoArticulo}, partida {Partida}",
                    dto.CodigoEmpresa, dto.CodigoArticulo, dto.LotePartida);

                return StatusCode(500, new ProblemDetails
                {
                    Title = "Error interno del servidor",
                    Detail = "Ocurrió un error al bloquear el stock",
                    Status = 500
                });
            }
        }

        /// <summary>
        /// Desbloquea stock específico
        /// </summary>
        /// <param name="dto">Datos del desbloqueo</param>
        /// <returns>Resultado del desbloqueo</returns>
        [HttpPost("desbloquear-stock")]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(typeof(ProblemDetails), 400)]
        [ProducesResponseType(typeof(ProblemDetails), 403)]
        [ProducesResponseType(typeof(ProblemDetails), 500)]
        public async Task<IActionResult> DesbloquearStock([FromBody] DesbloquearStockDto dto)
        {
            try
            {
                // 1. Obtener usuario desde token
                var usuarioId = await UsuarioHelper.ObtenerUsuarioDesdeTokenAsync(HttpContext, _auroraSgaContext);
                if (!usuarioId.HasValue)
                {
                    return Unauthorized(new ProblemDetails
                    {
                        Title = "No autorizado",
                        Detail = "Token de sesión inválido",
                        Status = 401
                    });
                }

                // 2. Verificar permiso 16 (Calidad)
                if (!await _calidadService.VerificarPermisoCalidadAsync(usuarioId.Value))
                {
                    return Unauthorized(new ProblemDetails
                    {
                        Title = "Sin permisos",
                        Detail = "No tiene permisos para acceder a Calidad",
                        Status = 403
                    });
                }

                // 3. Asignar usuario al DTO
                dto.UsuarioId = usuarioId.Value;

                // 4. Ejecutar desbloqueo
                var resultado = await _calidadService.DesbloquearStockAsync(dto);

                _logger.LogInformation("Desbloqueo de stock ejecutado para usuario {UsuarioId}, bloqueo ID {BloqueoId}",
                    usuarioId.Value, dto.IdBloqueo);

                return Ok(resultado);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en desbloqueo de stock para bloqueo ID {BloqueoId}",
                    dto.IdBloqueo);

                return StatusCode(500, new ProblemDetails
                {
                    Title = "Error interno del servidor",
                    Detail = "Ocurrió un error al desbloquear el stock",
                    Status = 500
                });
            }
        }

        /// <summary>
        /// Obtiene lista de bloqueos
        /// </summary>
        /// <param name="codigoEmpresa">Código de empresa</param>
        /// <param name="soloBloqueados">Si true, solo muestra bloqueos activos</param>
        /// <returns>Lista de bloqueos</returns>
        [HttpGet("bloqueos")]
        [ProducesResponseType(typeof(List<BloqueoCalidadDto>), 200)]
        [ProducesResponseType(typeof(ProblemDetails), 400)]
        [ProducesResponseType(typeof(ProblemDetails), 403)]
        [ProducesResponseType(typeof(ProblemDetails), 500)]
        public async Task<IActionResult> ObtenerBloqueos(
            [FromQuery] short codigoEmpresa,
            [FromQuery] bool? soloBloqueados = null)
        {
            try
            {
                // 1. Obtener usuario desde token
                var usuarioId = await UsuarioHelper.ObtenerUsuarioDesdeTokenAsync(HttpContext, _auroraSgaContext);
                if (!usuarioId.HasValue)
                {
                    return Unauthorized(new ProblemDetails
                    {
                        Title = "No autorizado",
                        Detail = "Token de sesión inválido",
                        Status = 401
                    });
                }

                // 2. Verificar permiso 16 (Calidad)
                if (!await _calidadService.VerificarPermisoCalidadAsync(usuarioId.Value))
                {
                    return Unauthorized(new ProblemDetails
                    {
                        Title = "Sin permisos",
                        Detail = "No tiene permisos para acceder a Calidad",
                        Status = 403
                    });
                }

                // 3. Verificar que el usuario tiene acceso a la empresa
                if (!await _calidadService.VerificarAccesoEmpresaAsync(usuarioId.Value, codigoEmpresa))
                {
                    return Unauthorized(new ProblemDetails
                    {
                        Title = "Sin acceso",
                        Detail = "No tiene acceso a esta empresa",
                        Status = 403
                    });
                }

                // 4. Obtener bloqueos
                var bloqueos = await _calidadService.ObtenerBloqueosAsync(codigoEmpresa, soloBloqueados);

                _logger.LogInformation("Consulta de bloqueos ejecutada para usuario {UsuarioId}, empresa {CodigoEmpresa}. Resultados: {Count}",
                    usuarioId.Value, codigoEmpresa, bloqueos.Count);

                return Ok(bloqueos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en consulta de bloqueos para empresa {CodigoEmpresa}",
                    codigoEmpresa);

                return StatusCode(500, new ProblemDetails
                {
                    Title = "Error interno del servidor",
                    Detail = "Ocurrió un error al consultar los bloqueos",
                    Status = 500
                });
            }
        }

        /// <summary>
        /// Endpoint de prueba SIN autenticación
        /// </summary>
        [HttpGet("test-stock")]
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
