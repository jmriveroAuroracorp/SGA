using System;

namespace SGA_Desktop.Models
{
    /// <summary>
    /// DTO para representar un destinatario de notificación (coincide con la API)
    /// </summary>
    public class NotificacionDestinatarioApiDto
    {
        /// <summary>
        /// Identificador único del destinatario
        /// </summary>
        public Guid IdDestinatario { get; set; }

        /// <summary>
        /// ID de la notificación relacionada
        /// </summary>
        public Guid IdNotificacion { get; set; }

        /// <summary>
        /// ID del usuario destinatario
        /// </summary>
        public int UsuarioId { get; set; }

        /// <summary>
        /// Nombre del usuario destinatario
        /// </summary>
        public string UsuarioNombre { get; set; } = string.Empty;

        /// <summary>
        /// Fecha de creación del destinatario
        /// </summary>
        public DateTime FechaCreacion { get; set; }

        /// <summary>
        /// Indica si el destinatario está activo
        /// </summary>
        public bool EsActiva { get; set; }

        /// <summary>
        /// Indica si el destinatario ha leído la notificación
        /// </summary>
        public bool Leida { get; set; }

        /// <summary>
        /// Fecha en que el destinatario leyó la notificación
        /// </summary>
        public DateTime? FechaLeida { get; set; }
    }
}

