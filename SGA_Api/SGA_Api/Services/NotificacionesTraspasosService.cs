using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using SGA_Api.Hubs;

namespace SGA_Api.Services
{
    /// <summary>
    /// Servicio para enviar notificaciones de traspasos a través de SignalR
    /// </summary>
    public class NotificacionesTraspasosService : INotificacionesTraspasosService
    {
        private readonly IHubContext<NotificacionesTraspasosHub> _hubContext;
        private readonly ILogger<NotificacionesTraspasosService> _logger;

        public NotificacionesTraspasosService(IHubContext<NotificacionesTraspasosHub> hubContext, ILogger<NotificacionesTraspasosService> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        /// <summary>
        /// Envía una notificación cuando cambia el estado de un traspaso
        /// </summary>
        /// <param name="traspasoId">ID del traspaso</param>
        /// <param name="nuevoEstado">Nuevo estado del traspaso</param>
        /// <param name="mensaje">Mensaje adicional opcional</param>
        public async Task NotificarCambioEstadoAsync(int traspasoId, string nuevoEstado, string? mensaje = null)
        {
            _logger.LogDebug("Enviando notificación de cambio de estado para traspaso {TraspasoId}: {NuevoEstado}", traspasoId, nuevoEstado);
            
            var notificacion = new
            {
                TraspasoId = traspasoId,
                TipoNotificacion = "CambioEstado",
                NuevoEstado = nuevoEstado,
                Mensaje = mensaje,
                Timestamp = DateTime.UtcNow
            };

            await _hubContext.Clients.Group($"Traspaso_{traspasoId}")
                .SendAsync("NotificacionTraspaso", notificacion);
        }

        /// <summary>
        /// Envía una notificación cuando se actualiza un traspaso
        /// </summary>
        /// <param name="traspasoId">ID del traspaso</param>
        /// <param name="tipoActualizacion">Tipo de actualización</param>
        /// <param name="datos">Datos adicionales de la actualización</param>
        public async Task NotificarActualizacionAsync(int traspasoId, string tipoActualizacion, object? datos = null)
        {
            _logger.LogDebug("Enviando notificación de actualización para traspaso {TraspasoId}: {TipoActualizacion}", traspasoId, tipoActualizacion);
            
            var notificacion = new
            {
                TraspasoId = traspasoId,
                TipoNotificacion = "Actualizacion",
                TipoActualizacion = tipoActualizacion,
                Datos = datos,
                Timestamp = DateTime.UtcNow
            };

            await _hubContext.Clients.Group($"Traspaso_{traspasoId}")
                .SendAsync("NotificacionTraspaso", notificacion);
        }

        /// <summary>
        /// Envía una notificación popup a un usuario específico
        /// </summary>
        public async Task NotificarPopupUsuarioAsync(int usuarioId, string titulo, string mensaje, string tipoNotificacion = "info")
        {
            _logger.LogDebug("Enviando notificación popup a usuario {UsuarioId}: {Titulo}", usuarioId, titulo);
            
            var notificacion = new
            {
                TipoNotificacion = "Popup",
                Titulo = titulo,
                Mensaje = mensaje,
                TipoPopup = tipoNotificacion,
                Timestamp = DateTime.UtcNow
            };

            await _hubContext.Clients.Group($"Usuario_{usuarioId}")
                .SendAsync("NotificacionUsuario", notificacion);
        }

        /// <summary>
        /// Envía una notificación personalizada a un usuario específico
        /// </summary>
        public async Task NotificarUsuarioAsync(int usuarioId, object notificacion)
        {
            _logger.LogDebug("Enviando notificación personalizada a usuario {UsuarioId}", usuarioId);
            
            await _hubContext.Clients.Group($"Usuario_{usuarioId}")
                .SendAsync("NotificacionUsuario", notificacion);
        }
    }
}
