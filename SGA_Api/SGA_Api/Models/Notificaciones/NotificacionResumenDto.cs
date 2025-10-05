namespace SGA_Api.Models.Notificaciones
{
    /// <summary>
    /// DTO para el resumen de notificaciones (lista principal)
    /// </summary>
    public class NotificacionResumenDto
    {
        /// <summary>
        /// Identificador único de la notificación
        /// </summary>
        public Guid IdNotificacion { get; set; }

        /// <summary>
        /// Tipo de notificación
        /// </summary>
        public string TipoNotificacion { get; set; } = string.Empty;

        /// <summary>
        /// Título de la notificación
        /// </summary>
        public string Titulo { get; set; } = string.Empty;

        /// <summary>
        /// Mensaje resumido (primeros caracteres)
        /// </summary>
        public string MensajeResumido { get; set; } = string.Empty;

        /// <summary>
        /// Fecha de creación de la notificación
        /// </summary>
        public DateTime FechaCreacion { get; set; }

        /// <summary>
        /// Indica si el usuario actual ha leído esta notificación
        /// </summary>
        public bool Leida { get; set; }

        /// <summary>
        /// Fecha en que el usuario actual leyó la notificación
        /// </summary>
        public DateTime? FechaLeida { get; set; }

        /// <summary>
        /// Estado actual del proceso relacionado
        /// </summary>
        public string? EstadoActual { get; set; }

        /// <summary>
        /// ID del proceso relacionado
        /// </summary>
        public Guid? ProcesoId { get; set; }

        /// <summary>
        /// Tipo de notificación para iconos (success, error, warning, info)
        /// </summary>
        public string TipoIcono => EstadoActual switch
        {
            "COMPLETADO" => "success",
            "ERROR_ERP" => "error",
            "PENDIENTE_ERP" => "warning",
            "PENDIENTE" => "info",
            _ => "info"
        };
    }
}
