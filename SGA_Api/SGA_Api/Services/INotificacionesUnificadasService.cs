using SGA_Api.Models.Notificaciones;

namespace SGA_Api.Services
{
    /// <summary>
    /// Interfaz para el servicio unificado de notificaciones que garantiza guardar en BD y enviar por SignalR
    /// </summary>
    public interface INotificacionesUnificadasService
    {
        /// <summary>
        /// Crea una notificación para un usuario específico, guardándola en BD y enviándola por SignalR
        /// </summary>
        /// <param name="usuarioId">ID del usuario destinatario</param>
        /// <param name="tipoNotificacion">Tipo de notificación (TRASPASO, CONTEO, etc.)</param>
        /// <param name="titulo">Título de la notificación</param>
        /// <param name="mensaje">Mensaje de la notificación</param>
        /// <param name="procesoId">ID del proceso relacionado (opcional)</param>
        /// <param name="estadoAnterior">Estado anterior del proceso (opcional)</param>
        /// <param name="estadoActual">Estado actual del proceso (opcional)</param>
        /// <param name="tipoVisual">Tipo visual (info, success, warning, error)</param>
        /// <returns>La notificación creada en BD</returns>
        Task<Notificacion> CrearYEnviarNotificacionUsuarioAsync(
            int usuarioId,
            string tipoNotificacion,
            string titulo,
            string mensaje,
            Guid? procesoId = null,
            string? estadoAnterior = null,
            string? estadoActual = null,
            string tipoVisual = "info");

        /// <summary>
        /// Crea notificaciones para usuarios con roles específicos, guardándolas en BD y enviándolas por SignalR
        /// </summary>
        /// <param name="roles">Array de roles (OPERARIO, SUPERVISOR, ADMIN)</param>
        /// <param name="tipoNotificacion">Tipo de notificación (TRASPASO, CONTEO, etc.)</param>
        /// <param name="titulo">Título de la notificación</param>
        /// <param name="mensaje">Mensaje de la notificación</param>
        /// <param name="procesoId">ID del proceso relacionado (opcional)</param>
        /// <param name="estadoAnterior">Estado anterior del proceso (opcional)</param>
        /// <param name="estadoActual">Estado actual del proceso (opcional)</param>
        /// <param name="tipoVisual">Tipo visual (info, success, warning, error)</param>
        /// <returns>Lista de notificaciones creadas en BD (una por cada usuario con los roles especificados)</returns>
        Task<List<Notificacion>> CrearYEnviarNotificacionRolesAsync(
            string[] roles,
            string tipoNotificacion,
            string titulo,
            string mensaje,
            Guid? procesoId = null,
            string? estadoAnterior = null,
            string? estadoActual = null,
            string tipoVisual = "info");

        /// <summary>
        /// Crea notificaciones para usuarios con nivel jerárquico igual o superior, guardándolas en BD y enviándolas por SignalR
        /// </summary>
        /// <param name="nivelMinimo">Nivel jerárquico mínimo requerido</param>
        /// <param name="tipoNotificacion">Tipo de notificación (TRASPASO, CONTEO, etc.)</param>
        /// <param name="titulo">Título de la notificación</param>
        /// <param name="mensaje">Mensaje de la notificación</param>
        /// <param name="procesoId">ID del proceso relacionado (opcional)</param>
        /// <param name="tipoVisual">Tipo visual (info, success, warning, error)</param>
        /// <returns>Lista de notificaciones creadas en BD</returns>
        Task<List<Notificacion>> CrearYEnviarNotificacionNivelAsync(
            int nivelMinimo,
            string tipoNotificacion,
            string titulo,
            string mensaje,
            Guid? procesoId = null,
            string tipoVisual = "info");
    }
}

