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
                return CreatedAtAction(nameof(ObtenerOrden), new { id = orden.Id }, orden);
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
        /// Obtener una orden de conteo por ID
        /// </summary>
        /// <param name="id">ID de la orden</param>
        /// <returns>Orden con sus detalles</returns>
        [HttpGet("ordenes/{id:long}")]
        [ProducesResponseType(typeof(OrdenDto), 200)]
        [ProducesResponseType(typeof(ProblemDetails), 404)]
        public async Task<IActionResult> ObtenerOrden(long id)
        {
            try
            {
                var orden = await _conteosService.ObtenerOrdenAsync(id);
                if (orden == null)
                {
                    return NotFound(new ProblemDetails
                    {
                        Title = "Orden no encontrada",
                        Detail = $"No se encontró la orden de conteo con ID {id}",
                        Status = 404
                    });
                }

                return Ok(orden);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener orden de conteo {Id}", id);
                return StatusCode(500, new ProblemDetails
                {
                    Title = "Error interno del servidor",
                    Detail = "Ocurrió un error al obtener la orden de conteo",
                    Status = 500
                });
            }
        }

        /// <summary>
        /// Listar órdenes de conteo con filtros opcionales
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
        /// Iniciar una orden de conteo
        /// </summary>
        /// <param name="id">ID de la orden</param>
        /// <param name="codigoOperario">Código del operario que inicia la orden</param>
        /// <returns>Orden actualizada</returns>
        [HttpPost("ordenes/{id:long}/start")]
        [ProducesResponseType(typeof(OrdenDto), 200)]
        [ProducesResponseType(typeof(ProblemDetails), 400)]
        [ProducesResponseType(typeof(ProblemDetails), 404)]
        public async Task<IActionResult> IniciarOrden(long id, [FromQuery] string codigoOperario)
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

                var orden = await _conteosService.IniciarOrdenAsync(id, codigoOperario);
                return Ok(orden);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Error de validación al iniciar orden {Id}", id);
                return BadRequest(new ProblemDetails
                {
                    Title = "Error de validación",
                    Detail = ex.Message,
                    Status = 400
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al iniciar orden de conteo {Id}", id);
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
        /// <param name="id">ID de la orden</param>
        /// <param name="dto">Datos de la asignación</param>
        /// <returns>Orden actualizada</returns>
        [HttpPost("ordenes/{id:long}/asignar")]
        [ProducesResponseType(typeof(OrdenDto), 200)]
        [ProducesResponseType(typeof(ProblemDetails), 400)]
        [ProducesResponseType(typeof(ProblemDetails), 404)]
        public async Task<IActionResult> AsignarOperario(long id, [FromBody] AsignarOperarioDto dto)
        {
            try
            {
                var orden = await _conteosService.AsignarOperarioAsync(id, dto);
                return Ok(orden);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Error de validación al asignar operario a orden {Id}", id);
                return BadRequest(new ProblemDetails
                {
                    Title = "Error de validación",
                    Detail = ex.Message,
                    Status = 400
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al asignar operario a orden de conteo {Id}", id);
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
        /// <param name="id">ID de la orden</param>
        /// <param name="dto">Datos de la lectura</param>
        /// <returns>Lectura creada</returns>
        [HttpPost("ordenes/{id:long}/lecturas")]
        [ProducesResponseType(typeof(LecturaResponseDto), 201)]
        [ProducesResponseType(typeof(ProblemDetails), 400)]
        [ProducesResponseType(typeof(ProblemDetails), 404)]
        public async Task<IActionResult> CrearLectura(long id, [FromBody] LecturaDto dto)
        {
            try
            {
                var lectura = await _conteosService.CrearLecturaAsync(id, dto);
                return CreatedAtAction(nameof(ObtenerOrden), new { id = id }, lectura);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Error de validación al crear lectura para orden {Id}", id);
                return BadRequest(new ProblemDetails
                {
                    Title = "Error de validación",
                    Detail = ex.Message,
                    Status = 400
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear lectura para orden {Id}", id);
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
        /// <param name="id">ID de la orden</param>
        /// <returns>Resultado del cierre de la orden</returns>
        [HttpPost("ordenes/{id:long}/cerrar")]
        [ProducesResponseType(typeof(CerrarOrdenResponseDto), 200)]
        [ProducesResponseType(typeof(ProblemDetails), 400)]
        [ProducesResponseType(typeof(ProblemDetails), 404)]
        public async Task<IActionResult> CerrarOrden(long id)
        {
            try
            {
                var resultado = await _conteosService.CerrarOrdenAsync(id);
                return Ok(resultado);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Error de validación al cerrar orden {Id}", id);
                return BadRequest(new ProblemDetails
                {
                    Title = "Error de validación",
                    Detail = ex.Message,
                    Status = 400
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cerrar orden de conteo {Id}", id);
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
        /// <param name="id">ID de la orden</param>
        /// <param name="codigoOperario">Código del operario (opcional)</param>
        /// <returns>Lista de lecturas pendientes</returns>
        [HttpGet("ordenes/{id:long}/lecturas-pendientes")]
        [ProducesResponseType(typeof(IEnumerable<LecturaResponseDto>), 200)]
        [ProducesResponseType(typeof(ProblemDetails), 404)]
        public async Task<IActionResult> ObtenerLecturasPendientes(long id, [FromQuery] string? codigoOperario = null)
        {
            try
            {
                var lecturas = await _conteosService.ObtenerLecturasPendientesAsync(id, codigoOperario);
                return Ok(lecturas);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener lecturas pendientes para orden {Id}: {Message}", id, ex.Message);
                return StatusCode(500, new ProblemDetails
                {
                    Title = "Error interno del servidor",
                    Detail = $"Ocurrió un error al obtener las lecturas pendientes: {ex.Message}",
                    Status = 500
                });
            }
        }
    }
} 