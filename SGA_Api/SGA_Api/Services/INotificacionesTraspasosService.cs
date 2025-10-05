using SGA_Api.Models.Traspasos;

namespace SGA_Api.Services
{
    /// <summary>
    /// Interfaz para el servicio de notificaciones de traspasos
    /// </summary>
    public interface INotificacionesTraspasosService
    {
        /// <summary>
        /// Envía una notificación cuando cambia el estado de un traspaso
        /// </summary>
        /// <param name="traspasoId">ID del traspaso</param>
        /// <param name="nuevoEstado">Nuevo estado del traspaso</param>
        /// <param name="mensaje">Mensaje adicional opcional</param>
        Task NotificarCambioEstadoAsync(int traspasoId, string nuevoEstado, string? mensaje = null);

        /// <summary>
        /// Envía una notificación cuando se actualiza un traspaso
        /// </summary>
        /// <param name="traspasoId">ID del traspaso</param>
        /// <param name="tipoActualizacion">Tipo de actualización</param>
        /// <param name="datos">Datos adicionales de la actualización</param>
        Task NotificarActualizacionAsync(int traspasoId, string tipoActualizacion, object? datos = null);

        /// <summary>
        /// Envía una notificación popup a un usuario específico
        /// </summary>
        /// <param name="usuarioId">ID del usuario destinatario</param>
        /// <param name="titulo">Título de la notificación</param>
        /// <param name="mensaje">Mensaje de la notificación</param>
        /// <param name="tipoNotificacion">Tipo de notificación (info, warning, error, success)</param>
        Task NotificarPopupUsuarioAsync(int usuarioId, string titulo, string mensaje, string tipoNotificacion = "info");

        /// <summary>
        /// Envía una notificación personalizada a un usuario específico
        /// </summary>
        /// <param name="usuarioId">ID del usuario destinatario</param>
        /// <param name="notificacion">Objeto de notificación personalizada</param>
        Task NotificarUsuarioAsync(int usuarioId, object notificacion);
    }
}
