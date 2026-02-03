using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using SGA_Api.Hubs;
using SGA_Api.Data;
using Microsoft.EntityFrameworkCore;

namespace SGA_Api.Services
{
    /// <summary>
    /// Servicio para enviar notificaciones de traspasos a través de SignalR
    /// </summary>
    public class NotificacionesTraspasosService : INotificacionesTraspasosService
    {
        private readonly IHubContext<NotificacionesTraspasosHub> _hubContext;
        private readonly ILogger<NotificacionesTraspasosService> _logger;
        private readonly AuroraSgaDbContext _context;

        public NotificacionesTraspasosService(IHubContext<NotificacionesTraspasosHub> hubContext, ILogger<NotificacionesTraspasosService> logger, AuroraSgaDbContext context)
        {
            _hubContext = hubContext;
            _logger = logger;
            _context = context;
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
                Timestamp = DateTime.Now
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
                Timestamp = DateTime.Now
            };

            await _hubContext.Clients.Group($"Traspaso_{traspasoId}")
                .SendAsync("NotificacionTraspaso", notificacion);
        }

        /// <summary>
        /// Envía una notificación popup a un usuario específico
        /// </summary>
        public async Task NotificarPopupUsuarioAsync(int usuarioId, string titulo, string mensaje, string tipoNotificacion = "info", string? tipoNotificacionReal = null, Guid? procesoId = null, int? codigoEmpresa = null)
        {
            _logger.LogInformation("🔔 [SignalR] Enviando notificación popup a usuario {UsuarioId}: {Titulo}", usuarioId, titulo);
            
            var notificacion = new
            {
                TipoNotificacion = tipoNotificacionReal ?? "Popup",
                Titulo = titulo,
                Mensaje = mensaje,
                TipoPopup = tipoNotificacion,
                ProcesoId = procesoId,
                CodigoEmpresa = codigoEmpresa ?? 1,
                Timestamp = DateTime.Now
            };

            try
            {
                var grupo = $"Usuario_{usuarioId}";
                _logger.LogInformation("🔔 [SignalR] Enviando a grupo: {Grupo}", grupo);
                
                await _hubContext.Clients.Group(grupo)
                    .SendAsync("NotificacionUsuario", notificacion);
                
                _logger.LogInformation("✅ [SignalR] Notificación enviada exitosamente a grupo {Grupo}", grupo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [SignalR] Error al enviar notificación a usuario {UsuarioId}", usuarioId);
                throw;
            }
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

        /// <summary>
        /// Envía una notificación a todos los usuarios con un rol específico
        /// </summary>
        public async Task NotificarRolAsync(string rolNombre, string titulo, string mensaje, string tipoNotificacion = "info")
        {
            _logger.LogInformation("🔔 ENVIANDO NOTIFICACIÓN A ROL: {RolNombre} - {Titulo}", rolNombre, titulo);
            
            var notificacion = new
            {
                TipoNotificacion = "Rol",
                RolDestino = rolNombre,
                Titulo = titulo,
                Mensaje = mensaje,
                TipoPopup = tipoNotificacion,
                Timestamp = DateTime.Now
            };

            try
            {
                await _hubContext.Clients.Group($"Rol_{rolNombre}")
                    .SendAsync("NotificacionRol", notificacion);
                
                _logger.LogInformation("✅ NOTIFICACIÓN ENVIADA EXITOSAMENTE a grupo Rol_{RolNombre}", rolNombre);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ ERROR AL ENVIAR NOTIFICACIÓN a grupo Rol_{RolNombre}", rolNombre);
            }
        }

        /// <summary>
        /// Envía una notificación a usuarios con roles de nivel jerárquico igual o superior
        /// </summary>
        public async Task NotificarNivelJerarquicoAsync(int nivelMinimo, string titulo, string mensaje, string tipoNotificacion = "info")
        {
            _logger.LogDebug("Enviando notificación a nivel jerárquico {NivelMinimo}+: {Titulo}", nivelMinimo, titulo);
            
            var notificacion = new
            {
                TipoNotificacion = "NivelJerarquico",
                NivelMinimo = nivelMinimo,
                Titulo = titulo,
                Mensaje = mensaje,
                TipoPopup = tipoNotificacion,
                Timestamp = DateTime.Now
            };

            // Enviar a todos los grupos de roles con nivel >= nivelMinimo
            var roles = new[] { "OPERARIO", "SUPERVISOR", "ADMIN" };
            var niveles = new[] { 10, 20, 30 };
            
            for (int i = 0; i < roles.Length; i++)
            {
                if (niveles[i] >= nivelMinimo)
                {
                    await _hubContext.Clients.Group($"Rol_{roles[i]}")
                        .SendAsync("NotificacionNivelJerarquico", notificacion);
                }
            }
        }

        /// <summary>
        /// Envía una notificación específica para conteos a supervisores y administradores
        /// </summary>
        public async Task NotificarEventoConteoAsync(string tipoEvento, Guid ordenId, string titulo, string mensaje, object? datosAdicionales = null)
        {
            _logger.LogDebug("Enviando notificación de evento de conteo {TipoEvento} para orden {OrdenId}: {Titulo}", tipoEvento, ordenId, titulo);
            
            var notificacion = new
            {
                TipoNotificacion = "EventoConteo",
                TipoEvento = tipoEvento,
                OrdenId = ordenId,
                Titulo = titulo,
                Mensaje = mensaje,
                DatosAdicionales = datosAdicionales,
                Timestamp = DateTime.Now
            };

            // Enviar a supervisores y administradores
            await _hubContext.Clients.Group("Rol_SUPERVISOR")
                .SendAsync("NotificacionEventoConteo", notificacion);
                
            await _hubContext.Clients.Group("Rol_ADMIN")
                .SendAsync("NotificacionEventoConteo", notificacion);
        }
    }
}
