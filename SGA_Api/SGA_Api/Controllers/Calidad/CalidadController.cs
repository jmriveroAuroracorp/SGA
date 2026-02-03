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
        /// 🔷 NUEVO: Consulta bloqueos de calidad por lista de artículos
        /// </summary>
        /// <param name="request">Lista de códigos de artículos</param>
        /// <returns>Diccionario con información de bloqueos</returns>
        [HttpPost("bloqueos-por-articulos")]
        [ProducesResponseType(typeof(Dictionary<string, object>), 200)]
        [ProducesResponseType(typeof(ProblemDetails), 400)]
        [ProducesResponseType(typeof(ProblemDetails), 500)]
        public async Task<IActionResult> ObtenerBloqueosPorArticulos([FromBody] BloqueosPorArticulosRequest request)
        {
            try
            {
                // 1. Validar parámetros
                if (request.CodigoEmpresa <= 0)
                    return BadRequest(new ProblemDetails
                    {
                        Title = "Parámetro inválido",
                        Detail = "Código de empresa es obligatorio",
                        Status = 400
                    });

                if (request.CodigosArticulos == null || !request.CodigosArticulos.Any())
                    return BadRequest(new ProblemDetails
                    {
                        Title = "Parámetro inválido",
                        Detail = "Lista de códigos de artículos es obligatoria",
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
                if (!await _calidadService.VerificarAccesoEmpresaAsync(usuarioId.Value, request.CodigoEmpresa))
                {
                    return Unauthorized(new ProblemDetails
                    {
                        Title = "Sin acceso",
                        Detail = "No tiene acceso a esta empresa",
                        Status = 403
                    });
                }

                // 5. Consultar bloqueos
                var bloqueos = await _calidadService.ObtenerBloqueosPorArticulosAsync(
                    request.CodigoEmpresa, request.CodigosArticulos);

                _logger.LogInformation("Consulta de bloqueos por artículos ejecutada para usuario {UsuarioId}, empresa {CodigoEmpresa}. Artículos: {Count}",
                    usuarioId.Value, request.CodigoEmpresa, request.CodigosArticulos.Count);

                return Ok(bloqueos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en consulta de bloqueos por artículos para empresa {CodigoEmpresa}",
                    request.CodigoEmpresa);

                return StatusCode(500, new ProblemDetails
                {
                    Title = "Error interno del servidor",
                    Detail = "Ocurrió un error al consultar los bloqueos",
                    Status = 500
                });
            }
        }

        /// <summary>
        /// Obtiene estadísticas de bloqueos de calidad
        /// </summary>
        /// <param name="codigoEmpresa">Código de empresa</param>
        /// <returns>Estadísticas de bloqueos</returns>
        [HttpGet("estadisticas")]
        [ProducesResponseType(typeof(EstadisticasCalidadDto), 200)]
        [ProducesResponseType(typeof(ProblemDetails), 400)]
        [ProducesResponseType(typeof(ProblemDetails), 403)]
        [ProducesResponseType(typeof(ProblemDetails), 500)]
        public async Task<IActionResult> ObtenerEstadisticas([FromQuery] short codigoEmpresa)
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

                // 4. Obtener estadísticas
                var estadisticas = await _calidadService.ObtenerEstadisticasAsync(codigoEmpresa);

                _logger.LogInformation("Consulta de estadísticas ejecutada para usuario {UsuarioId}, empresa {CodigoEmpresa}",
                    usuarioId.Value, codigoEmpresa);

                return Ok(estadisticas);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en consulta de estadísticas para empresa {CodigoEmpresa}",
                    codigoEmpresa);

                return StatusCode(500, new ProblemDetails
                {
                    Title = "Error interno del servidor",
                    Detail = "Ocurrió un error al consultar las estadísticas",
                    Status = 500
                });
            }
        }

    }
}
