using Microsoft.AspNetCore.Mvc;
using SGA_Api.Services;
using SGA_Api.Models.Notificaciones;

namespace SGA_Api.Controllers.Notificaciones
{
    [ApiController]
    [Route("api/[controller]")]
    public class NotificacionesController : ControllerBase
    {
        private readonly INotificacionesService _notificacionesService;
        private readonly ILogger<NotificacionesController> _logger;

        public NotificacionesController(INotificacionesService notificacionesService, ILogger<NotificacionesController> logger)
        {
            _notificacionesService = notificacionesService;
            _logger = logger;
        }

        /// <summary>
        /// Obtiene todas las notificaciones pendientes de un usuario
        /// </summary>
        /// <param name="usuarioId">ID del usuario</param>
        /// <returns>Lista de notificaciones pendientes</returns>
        [HttpGet("{usuarioId}")]
        public async Task<IActionResult> ObtenerNotificacionesPendientes(int usuarioId)
        {
            try
            {
                _logger.LogDebug("🔔 Obteniendo notificaciones pendientes para usuario {UsuarioId}", usuarioId);

                var notificaciones = await _notificacionesService.ObtenerNotificacionesUsuarioAsync(usuarioId);

                _logger.LogDebug("✅ Se encontraron {Cantidad} notificaciones para usuario {UsuarioId}", 
                    notificaciones.Count, usuarioId);

                return Ok(new
                {
                    success = true,
                    data = notificaciones,
                    count = notificaciones.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error al obtener notificaciones para usuario {UsuarioId}", usuarioId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error interno del servidor al obtener notificaciones",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Obtiene un resumen de las notificaciones de un usuario (para listas optimizadas)
        /// </summary>
        /// <param name="usuarioId">ID del usuario</param>
        /// <returns>Resumen de notificaciones</returns>
        [HttpGet("{usuarioId}/resumen")]
        public async Task<IActionResult> ObtenerResumenNotificaciones(int usuarioId)
        {
            try
            {
                _logger.LogDebug("📋 Obteniendo resumen de notificaciones para usuario {UsuarioId}", usuarioId);

                var resumen = await _notificacionesService.ObtenerResumenNotificacionesUsuarioAsync(usuarioId);

                _logger.LogDebug("✅ Resumen obtenido: {Cantidad} notificaciones para usuario {UsuarioId}", 
                    resumen.Count, usuarioId);

                return Ok(new
                {
                    success = true,
                    data = resumen,
                    count = resumen.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error al obtener resumen de notificaciones para usuario {UsuarioId}", usuarioId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error interno del servidor al obtener resumen de notificaciones",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Marca una notificación específica como leída
        /// </summary>
        /// <param name="idNotificacion">ID de la notificación</param>
        /// <param name="usuarioId">ID del usuario</param>
        /// <returns>Resultado de la operación</returns>
        [HttpPost("{idNotificacion}/marcar-leida")]
        public async Task<IActionResult> MarcarComoLeida(Guid idNotificacion, [FromBody] MarcarLeidaDto request)
        {
            try
            {
                _logger.LogDebug("✅ Marcando notificación {IdNotificacion} como leída para usuario {UsuarioId}", 
                    idNotificacion, request.UsuarioId);

                var resultado = await _notificacionesService.MarcarComoLeidaAsync(idNotificacion, request.UsuarioId);

                if (resultado)
                {
                    _logger.LogDebug("✅ Notificación {IdNotificacion} marcada como leída exitosamente", idNotificacion);
                    return Ok(new
                    {
                        success = true,
                        message = "Notificación marcada como leída correctamente"
                    });
                }
                else
                {
                    _logger.LogWarning("⚠️ No se pudo marcar la notificación {IdNotificacion} como leída", idNotificacion);
                    return NotFound(new
                    {
                        success = false,
                        message = "No se encontró la notificación o no tienes permisos para marcarla como leída"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error al marcar notificación {IdNotificacion} como leída para usuario {UsuarioId}", 
                    idNotificacion, request.UsuarioId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error interno del servidor al marcar la notificación como leída",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Marca todas las notificaciones de un usuario como leídas
        /// </summary>
        /// <param name="usuarioId">ID del usuario</param>
        /// <returns>Resultado de la operación</returns>
        [HttpPost("marcar-todas-leidas")]
        public async Task<IActionResult> MarcarTodasComoLeidas([FromBody] int usuarioId)
        {
            try
            {
                _logger.LogDebug("✅ Marcando todas las notificaciones como leídas para usuario {UsuarioId}", usuarioId);

                var resultado = await _notificacionesService.MarcarTodasComoLeidasAsync(usuarioId);

                _logger.LogDebug("✅ Se marcaron {Cantidad} notificaciones como leídas para usuario {UsuarioId}", 
                    resultado, usuarioId);

                return Ok(new
                {
                    success = true,
                    message = $"Se marcaron {resultado} notificaciones como leídas",
                    count = resultado
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error al marcar todas las notificaciones como leídas para usuario {UsuarioId}", usuarioId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error interno del servidor al marcar las notificaciones como leídas",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Obtiene el contador de notificaciones pendientes de un usuario
        /// </summary>
        /// <param name="usuarioId">ID del usuario</param>
        /// <returns>Contador de notificaciones pendientes</returns>
        [HttpGet("{usuarioId}/contador")]
        public async Task<IActionResult> ObtenerContadorPendientes(int usuarioId)
        {
            try
            {
                _logger.LogDebug("🔢 Obteniendo contador de notificaciones pendientes para usuario {UsuarioId}", usuarioId);

                var contador = await _notificacionesService.ObtenerConteoNoLeidasAsync(usuarioId);

                _logger.LogDebug("✅ Contador obtenido: {Contador} notificaciones pendientes para usuario {UsuarioId}", 
                    contador, usuarioId);

                return Ok(new
                {
                    success = true,
                    data = new { contador },
                    count = contador
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error al obtener contador de notificaciones para usuario {UsuarioId}", usuarioId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error interno del servidor al obtener el contador de notificaciones",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Elimina una notificación específica
        /// </summary>
        /// <param name="idNotificacion">ID de la notificación</param>
        /// <param name="usuarioId">ID del usuario</param>
        /// <returns>Resultado de la operación</returns>
        [HttpDelete("{idNotificacion}")]
        public async Task<IActionResult> EliminarNotificacion(Guid idNotificacion, [FromQuery] int usuarioId)
        {
            try
            {
                _logger.LogDebug("🗑️ Eliminando notificación {IdNotificacion} para usuario {UsuarioId}", 
                    idNotificacion, usuarioId);

                var resultado = await _notificacionesService.EliminarNotificacionAsync(idNotificacion, usuarioId);

                if (resultado)
                {
                    _logger.LogDebug("✅ Notificación {IdNotificacion} eliminada exitosamente", idNotificacion);
                    return Ok(new
                    {
                        success = true,
                        message = "Notificación eliminada correctamente"
                    });
                }
                else
                {
                    _logger.LogWarning("⚠️ No se pudo eliminar la notificación {IdNotificacion}", idNotificacion);
                    return NotFound(new
                    {
                        success = false,
                        message = "No se encontró la notificación o no tienes permisos para eliminarla"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error al eliminar notificación {IdNotificacion} para usuario {UsuarioId}", 
                    idNotificacion, usuarioId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error interno del servidor al eliminar la notificación",
                    error = ex.Message
                });
            }
        }
    }
}
