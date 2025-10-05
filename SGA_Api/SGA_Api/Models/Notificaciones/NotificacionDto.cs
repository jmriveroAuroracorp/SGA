namespace SGA_Api.Models.Notificaciones
{
    /// <summary>
    /// DTO para representar una notificación completa con información de lectura
    /// </summary>
    public class NotificacionDto
    {
        /// <summary>
        /// Identificador único de la notificación
        /// </summary>
        public Guid IdNotificacion { get; set; }

        /// <summary>
        /// Código de la empresa
        /// </summary>
        public short CodigoEmpresa { get; set; }

        /// <summary>
        /// Tipo de notificación
        /// </summary>
        public string TipoNotificacion { get; set; } = string.Empty;

        /// <summary>
        /// ID del proceso relacionado
        /// </summary>
        public Guid? ProcesoId { get; set; }

        /// <summary>
        /// Título de la notificación
        /// </summary>
        public string Titulo { get; set; } = string.Empty;

        /// <summary>
        /// Mensaje detallado de la notificación
        /// </summary>
        public string Mensaje { get; set; } = string.Empty;

        /// <summary>
        /// Estado anterior del proceso
        /// </summary>
        public string? EstadoAnterior { get; set; }

        /// <summary>
        /// Estado actual del proceso
        /// </summary>
        public string? EstadoActual { get; set; }

        /// <summary>
        /// Fecha de creación de la notificación
        /// </summary>
        public DateTime FechaCreacion { get; set; }

        /// <summary>
        /// Indica si la notificación está activa
        /// </summary>
        public bool EsActiva { get; set; }

        /// <summary>
        /// Indica si la notificación es para un grupo de usuarios
        /// </summary>
        public bool EsGrupal { get; set; }

        /// <summary>
        /// Grupo destino para notificaciones grupales
        /// </summary>
        public string? GrupoDestino { get; set; }

        /// <summary>
        /// Comentario adicional de la notificación
        /// </summary>
        public string? Comentario { get; set; }

        /// <summary>
        /// Indica si el usuario actual ha leído esta notificación
        /// </summary>
        public bool Leida { get; set; }

        /// <summary>
        /// Fecha en que el usuario actual leyó la notificación (si la ha leído)
        /// </summary>
        public DateTime? FechaLeida { get; set; }

        /// <summary>
        /// Lista de destinatarios de la notificación
        /// </summary>
        public List<NotificacionDestinatarioDto> Destinatarios { get; set; } = new List<NotificacionDestinatarioDto>();

        /// <summary>
        /// Información adicional para el tipo de notificación (ej: datos del traspaso, inventario, etc.)
        /// </summary>
        public object? DatosAdicionales { get; set; }
    }
}
