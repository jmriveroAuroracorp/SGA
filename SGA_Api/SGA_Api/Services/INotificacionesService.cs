using SGA_Api.Models.Notificaciones;

namespace SGA_Api.Services
{
    /// <summary>
    /// Interface para el servicio de gestión de notificaciones en base de datos
    /// </summary>
    public interface INotificacionesService
    {
        /// <summary>
        /// Crea una nueva notificación en la base de datos
        /// </summary>
        Task<Notificacion> CrearNotificacionAsync(CrearNotificacionDto crearDto);

        /// <summary>
        /// Obtiene las notificaciones de un usuario específico
        /// </summary>
        Task<List<NotificacionDto>> ObtenerNotificacionesUsuarioAsync(int usuarioId, bool soloNoLeidas = false, int? limit = null);

        /// <summary>
        /// Obtiene el resumen de notificaciones para un usuario
        /// </summary>
        Task<List<NotificacionResumenDto>> ObtenerResumenNotificacionesUsuarioAsync(int usuarioId, int? limit = null);

        /// <summary>
        /// Marca una notificación como leída para un usuario específico
        /// </summary>
        Task<bool> MarcarComoLeidaAsync(Guid idNotificacion, int usuarioId);

        /// <summary>
        /// Marca múltiples notificaciones como leídas para un usuario específico
        /// </summary>
        Task<int> MarcarMultiplesComoLeidasAsync(List<Guid> idNotificaciones, int usuarioId);

        /// <summary>
        /// Obtiene el conteo de notificaciones no leídas para un usuario
        /// </summary>
        Task<int> ObtenerConteoNoLeidasAsync(int usuarioId);

        /// <summary>
        /// Obtiene una notificación específica por ID
        /// </summary>
        Task<NotificacionDto?> ObtenerNotificacionPorIdAsync(Guid idNotificacion, int usuarioId);

        /// <summary>
        /// Elimina una notificación (soft delete - marca como inactiva)
        /// </summary>
        Task<bool> EliminarNotificacionAsync(Guid idNotificacion);

        /// <summary>
        /// Obtiene notificaciones por tipo y proceso
        /// </summary>
        Task<List<NotificacionDto>> ObtenerNotificacionesPorProcesoAsync(string tipoNotificacion, Guid procesoId);

        /// <summary>
        /// Crea una notificación de traspaso con destinatarios automáticos
        /// </summary>
        Task<Notificacion> CrearNotificacionTraspasoAsync(Guid traspasoId, string titulo, string mensaje, string estadoAnterior, string estadoActual, int usuarioDestinatario);

        /// <summary>
        /// Crea una notificación grupal
        /// </summary>
        Task<Notificacion> CrearNotificacionGrupalAsync(string tipoNotificacion, string titulo, string mensaje, string grupoDestino, List<int> usuarioIds);
    }
}
