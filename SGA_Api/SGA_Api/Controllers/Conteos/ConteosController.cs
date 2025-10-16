using Microsoft.AspNetCore.Mvc;
using SGA_Api.Models.Conteos;
using SGA_Api.Services;

namespace SGA_Api.Controllers
{
    /// <summary>
    /// Controlador para gestionar conteos rotativos
    /// </summary>
    [ApiController]
    [Route("api/conteos")]
    public class ConteosController : ControllerBase
    {
        private readonly IConteosService _conteosService;
        private readonly ILogger<ConteosController> _logger;

        public ConteosController(IConteosService conteosService, ILogger<ConteosController> logger)
        {
            _conteosService = conteosService;
            _logger = logger;
        }

        /// <summary>
        /// Crear una nueva orden de conteo rotativo
        /// </summary>
        /// <param name="dto">Datos de la orden a crear</param>
        /// <returns>Orden creada con su ID</returns>
        [HttpPost("ordenes")]
        [ProducesResponseType(typeof(OrdenDto), 201)]
        [ProducesResponseType(typeof(ProblemDetails), 400)]
        public async Task<IActionResult> CrearOrden([FromBody] CrearOrdenDto dto)
        {
            try
            {
                var orden = await _conteosService.CrearOrdenAsync(dto);
                return CreatedAtAction(nameof(ObtenerOrden), new { guid = orden.GuidID }, orden);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear orden de conteo");
                return StatusCode(500, new ProblemDetails
                {
                    Title = "Error interno del servidor",
                    Detail = "Ocurrió un error al crear la orden de conteo",
                    Status = 500
                });
            }
        }

        /// <summary>
        /// Actualizar una orden de conteo existente
        /// </summary>
        /// <param name="guid">Guid de la orden</param>
        /// <param name="dto">Datos actualizados de la orden</param>
        /// <returns>Orden actualizada</returns>
        [HttpPut("ordenes/{guid:guid}")]
        [ProducesResponseType(typeof(OrdenDto), 200)]
        [ProducesResponseType(typeof(ProblemDetails), 400)]
        [ProducesResponseType(typeof(ProblemDetails), 404)]
        public async Task<IActionResult> ActualizarOrden(Guid guid, [FromBody] CrearOrdenConteoDto dto)
        {
            try
            {
                var orden = await _conteosService.ActualizarOrdenAsync(guid, dto);
                return Ok(orden);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Error de validación al actualizar orden {Guid}", guid);
                return BadRequest(new ProblemDetails
                {
                    Title = "Error de validación",
                    Detail = ex.Message,
                    Status = 400
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar orden de conteo {Guid}", guid);
                return StatusCode(500, new ProblemDetails
                {
                    Title = "Error interno del servidor",
                    Detail = "Ocurrió un error al actualizar la orden de conteo",
                    Status = 500
                });
            }
        }

        /// <summary>
        /// Obtener una orden de conteo por Guid
        /// </summary>
        /// <param name="guid">Guid de la orden</param>
        /// <returns>Orden con sus detalles</returns>
        [HttpGet("ordenes/{guid:guid}")]
        [ProducesResponseType(typeof(OrdenDto), 200)]
        [ProducesResponseType(typeof(ProblemDetails), 404)]
        public async Task<IActionResult> ObtenerOrden(Guid guid)
        {
            try
            {
                var orden = await _conteosService.ObtenerOrdenAsync(guid);
                if (orden == null)
                {
                    return NotFound(new ProblemDetails
                    {
                        Title = "Orden no encontrada",
                        Detail = $"No se encontró la orden de conteo con Guid {guid}",
                        Status = 404
                    });
                }

                return Ok(orden);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener orden de conteo {Guid}", guid);
                return StatusCode(500, new ProblemDetails
                {
                    Title = "Error interno del servidor",
                    Detail = "Ocurrió un error al obtener la orden de conteo",
                    Status = 500
                });
            }
        }

        /// <summary>
        /// Listar órdenes de conteo con filtros opcionales (para Mobility)
        /// </summary>
        /// <param name="codigoOperario">Código del operario para filtrar</param>
        /// <param name="estado">Estado de la orden para filtrar</param>
        /// <returns>Lista de órdenes que cumplen con los filtros</returns>
        [HttpGet("ordenes")]
        [ProducesResponseType(typeof(IEnumerable<OrdenDto>), 200)]
        public async Task<IActionResult> ListarOrdenes(
            [FromQuery] string? codigoOperario = null,
            [FromQuery] string? estado = null)
        {
            try
            {
                var ordenes = await _conteosService.ListarOrdenesAsync(codigoOperario, estado);
                return Ok(ordenes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al listar órdenes de conteo");
                return StatusCode(500, new ProblemDetails
                {
                    Title = "Error interno del servidor",
                    Detail = "Ocurrió un error al listar las órdenes de conteo",
                    Status = 500
                });
            }
        }

        /// <summary>
        /// Listar todas las órdenes de conteo con filtros opcionales (para Desktop)
        /// </summary>
        /// <param name="estado">Estado de la orden para filtrar</param>
        /// <param name="codigoOperario">Código del operario para filtrar</param>
        /// <returns>Lista de todas las órdenes que cumplen con los filtros</returns>
        [HttpGet("ordenes/todas")]
        [ProducesResponseType(typeof(IEnumerable<OrdenDto>), 200)]
        public async Task<IActionResult> ListarTodasLasOrdenes([FromQuery] string? estado = null, [FromQuery] string? codigoOperario = null)
        {
            try
            {
                var ordenes = await _conteosService.ListarTodasLasOrdenesAsync(estado, codigoOperario);
                return Ok(ordenes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al listar todas las órdenes de conteo");
                return StatusCode(500, new ProblemDetails
                {
                    Title = "Error interno del servidor",
                    Detail = "Ocurrió un error al listar todas las órdenes de conteo",
                    Status = 500
                });
            }
        }

        /// <summary>
        /// Iniciar una orden de conteo
        /// </summary>
        /// <param name="guid">Guid de la orden</param>
        /// <param name="codigoOperario">Código del operario que inicia la orden</param>
        /// <returns>Orden actualizada</returns>
        [HttpPost("ordenes/{guid:guid}/start")]
        [ProducesResponseType(typeof(OrdenDto), 200)]
        [ProducesResponseType(typeof(ProblemDetails), 400)]
        [ProducesResponseType(typeof(ProblemDetails), 404)]
        public async Task<IActionResult> IniciarOrden(Guid guid, [FromQuery] string codigoOperario)
        {
            try
            {
                if (string.IsNullOrEmpty(codigoOperario))
                {
                    return BadRequest(new ProblemDetails
                    {
                        Title = "Parámetro requerido",
                        Detail = "El código del operario es obligatorio",
                        Status = 400
                    });
                }

                var orden = await _conteosService.IniciarOrdenAsync(guid, codigoOperario);
                return Ok(orden);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Error de validación al iniciar orden {Guid}", guid);
                return BadRequest(new ProblemDetails
                {
                    Title = "Error de validación",
                    Detail = ex.Message,
                    Status = 400
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al iniciar orden de conteo {Guid}", guid);
                return StatusCode(500, new ProblemDetails
                {
                    Title = "Error interno del servidor",
                    Detail = "Ocurrió un error al iniciar la orden de conteo",
                    Status = 500
                });
            }
        }

        /// <summary>
        /// Asignar un operario a una orden de conteo
        /// </summary>
        /// <param name="guid">Guid de la orden</param>
        /// <param name="dto">Datos de la asignación</param>
        /// <returns>Orden actualizada</returns>
        [HttpPost("ordenes/{guid:guid}/asignar")]
        [ProducesResponseType(typeof(OrdenDto), 200)]
        [ProducesResponseType(typeof(ProblemDetails), 400)]
        [ProducesResponseType(typeof(ProblemDetails), 404)]
        public async Task<IActionResult> AsignarOperario(Guid guid, [FromBody] AsignarOperarioDto dto)
        {
            try
            {
                var orden = await _conteosService.AsignarOperarioAsync(guid, dto);
                return Ok(orden);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Error de validación al asignar operario a orden {Guid}", guid);
                return BadRequest(new ProblemDetails
                {
                    Title = "Error de validación",
                    Detail = ex.Message,
                    Status = 400
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al asignar operario a orden de conteo {Guid}", guid);
                return StatusCode(500, new ProblemDetails
                {
                    Title = "Error interno del servidor",
                    Detail = "Ocurrió un error al asignar el operario a la orden de conteo",
                    Status = 500
                });
            }
        }

        /// <summary>
        /// Crear una nueva lectura de conteo
        /// </summary>
        /// <param name="guid">Guid de la orden</param>
        /// <param name="dto">Datos de la lectura</param>
        /// <returns>Lectura creada</returns>
        [HttpPost("ordenes/{guid:guid}/lecturas")]
        [ProducesResponseType(typeof(LecturaResponseDto), 201)]
        [ProducesResponseType(typeof(ProblemDetails), 400)]
        [ProducesResponseType(typeof(ProblemDetails), 404)]
        public async Task<IActionResult> CrearLectura(Guid guid, [FromBody] LecturaDto dto)
        {
            try
            {
                var lectura = await _conteosService.CrearLecturaAsync(guid, dto);
                return CreatedAtAction(nameof(ObtenerOrden), new { guid = guid }, lectura);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Error de validación al crear lectura para orden {Guid}", guid);
                return BadRequest(new ProblemDetails
                {
                    Title = "Error de validación",
                    Detail = ex.Message,
                    Status = 400
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear lectura para orden {Guid}", guid);
                return StatusCode(500, new ProblemDetails
                {
                    Title = "Error interno del servidor",
                    Detail = "Ocurrió un error al crear la lectura de conteo",
                    Status = 500
                });
            }
        }

        /// <summary>
        /// Cerrar una orden de conteo
        /// </summary>
        /// <param name="guid">Guid de la orden</param>
        /// <returns>Resultado del cierre de la orden</returns>
        [HttpPost("ordenes/{guid:guid}/cerrar")]
        [ProducesResponseType(typeof(CerrarOrdenResponseDto), 200)]
        [ProducesResponseType(typeof(ProblemDetails), 400)]
        [ProducesResponseType(typeof(ProblemDetails), 404)]
        public async Task<IActionResult> CerrarOrden(Guid guid)
        {
            try
            {
                var resultado = await _conteosService.CerrarOrdenAsync(guid);
                return Ok(resultado);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Error de validación al cerrar orden {Guid}", guid);
                return BadRequest(new ProblemDetails
                {
                    Title = "Error de validación",
                    Detail = ex.Message,
                    Status = 400
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cerrar orden de conteo {Guid}", guid);
                return StatusCode(500, new ProblemDetails
                {
                    Title = "Error interno del servidor",
                    Detail = "Ocurrió un error al cerrar la orden de conteo",
                    Status = 500
                });
            }
        }

        /// <summary>
        /// Obtener las lecturas pendientes de una orden
        /// </summary>
        /// <param name="guid">Guid de la orden</param>
        /// <param name="codigoOperario">Código del operario (opcional)</param>
        /// <returns>Lista de lecturas pendientes</returns>
        [HttpGet("ordenes/{guid:guid}/lecturas-pendientes")]
        [ProducesResponseType(typeof(IEnumerable<LecturaResponseDto>), 200)]
        [ProducesResponseType(typeof(ProblemDetails), 404)]
        public async Task<IActionResult> ObtenerLecturasPendientes(Guid guid, [FromQuery] string? codigoOperario = null)
        {
            try
            {
                var lecturas = await _conteosService.ObtenerLecturasPendientesAsync(guid, codigoOperario);
                return Ok(lecturas);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener lecturas pendientes para orden {Guid}: {Message}", guid, ex.Message);
                return StatusCode(500, new ProblemDetails
                {
                    Title = "Error interno del servidor",
                    Detail = $"Ocurrió un error al obtener las lecturas pendientes: {ex.Message}",
                    Status = 500
                });
            }
        }

        /// <summary>
        /// Obtener resultados de conteo con filtro opcional por acción
        /// </summary>
        /// <param name="accion">Filtro por acción (SUPERVISION, AJUSTE) - opcional</param>
        /// <returns>Lista de resultados de conteo ordenados por fecha más reciente</returns>
        [HttpGet("resultados")]
        [ProducesResponseType(typeof(IEnumerable<ResultadoConteoDetalladoDto>), 200)]
        public async Task<IActionResult> ObtenerResultadosConteo([FromQuery] string? accion = null)
        {
            try
            {
                var resultados = await _conteosService.ObtenerResultadosConteoAsync(accion);
                return Ok(resultados);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener resultados de conteo con filtro {Accion}: {Message}", accion, ex.Message);
                return StatusCode(500, new ProblemDetails
                {
                    Title = "Error interno del servidor",
                    Detail = "Ocurrió un error al obtener los resultados de conteo",
                    Status = 500
                });
            }
        }

        /// <summary>
        /// Actualizar el aprobador de un resultado de conteo (solo para SUPERVISION)
        /// </summary>
        /// <param name="resultadoGuid">GuidID del ResultadoConteo</param>
        /// <param name="dto">Datos del aprobador</param>
        /// <returns>Resultado de conteo actualizado</returns>
        [HttpPost("resultados/{resultadoGuid:guid}/aprobador")]
        [ProducesResponseType(typeof(ResultadoConteoDetalladoDto), 200)]
        [ProducesResponseType(typeof(ProblemDetails), 400)]
        [ProducesResponseType(typeof(ProblemDetails), 404)]
        public async Task<IActionResult> ActualizarAprobador(Guid resultadoGuid, [FromBody] ActualizarAprobadorDto dto)
        {
            try
            {
                var resultado = await _conteosService.ActualizarAprobadorAsync(resultadoGuid, dto);
                return Ok(resultado);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Error de validación al actualizar aprobador para resultado {ResultadoGuid}", resultadoGuid);
                return BadRequest(new ProblemDetails
                {
                    Title = "Error de validación",
                    Detail = ex.Message,
                    Status = 400
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar aprobador para resultado {ResultadoGuid}: {Message}", resultadoGuid, ex.Message);
                return StatusCode(500, new ProblemDetails
                {
                    Title = "Error interno del servidor",
                    Detail = "Ocurrió un error al actualizar el aprobador del resultado de conteo",
                    Status = 500
                });
            }
        }

        /// <summary>
        /// Reasignar una línea de conteo creando una nueva orden automáticamente
        /// </summary>
        /// <param name="resultadoGuid">GuidID del ResultadoConteo</param>
        /// <param name="dto">Datos de la reasignación</param>
        /// <returns>Nueva orden creada para la reasignación</returns>
        [HttpPost("resultados/{resultadoGuid:guid}/reasignar")]
        [ProducesResponseType(typeof(OrdenDto), 201)]
        [ProducesResponseType(typeof(ProblemDetails), 400)]
        [ProducesResponseType(typeof(ProblemDetails), 404)]
        public async Task<IActionResult> ReasignarLinea(Guid resultadoGuid, [FromBody] ReasignarLineaDto dto)
        {
            try
            {
                var nuevaOrden = await _conteosService.ReasignarLineaAsync(resultadoGuid, dto);
                return CreatedAtAction(nameof(ObtenerOrden), new { guid = nuevaOrden.GuidID }, nuevaOrden);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Error de validación al reasignar línea para resultado {ResultadoGuid}", resultadoGuid);
                return BadRequest(new ProblemDetails
                {
                    Title = "Error de validación",
                    Detail = ex.Message,
                    Status = 400
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al reasignar línea para resultado {ResultadoGuid}: {Message}", resultadoGuid, ex.Message);
                return StatusCode(500, new ProblemDetails
                {
                    Title = "Error interno del servidor",
                    Detail = "Ocurrió un error al reasignar la línea de conteo",
                    Status = 500
                });
            }
        }

        /// <summary>
        /// Obtiene todos los palets disponibles en una ubicación específica
        /// </summary>
        /// <param name="codigoAlmacen">Código del almacén</param>
        /// <param name="ubicacion">Código de la ubicación</param>
        /// <param name="codigoArticulo">Código del artículo</param>
        /// <param name="lote">Lote/partida (opcional)</param>
        /// <param name="fechaCaducidad">Fecha de caducidad (opcional)</param>
        /// <returns>Lista de palets disponibles</returns>
        [HttpGet("palets-disponibles")]
        public async Task<IActionResult> ObtenerPaletsDisponibles(
            [FromQuery] string codigoAlmacen,
            [FromQuery] string ubicacion,
            [FromQuery] string codigoArticulo,
            [FromQuery] string? lote = null,
            [FromQuery] DateTime? fechaCaducidad = null)
        {
            try
            {
                var palets = await _conteosService.ObtenerPaletsDisponiblesAsync(
                    codigoAlmacen, 
                    ubicacion, 
                    codigoArticulo, 
                    lote, 
                    fechaCaducidad);
                
                return Ok(palets);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo palets disponibles para {Ubicacion}/{Articulo}", ubicacion, codigoArticulo);
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }
    }
} 