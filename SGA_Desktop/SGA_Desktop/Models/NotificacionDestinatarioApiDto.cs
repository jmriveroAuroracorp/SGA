using System;
using System.Text.Json.Serialization;

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
        [JsonPropertyName("idDestinatario")]
        public Guid IdDestinatario { get; set; }

        /// <summary>
        /// ID de la notificación relacionada
        /// </summary>
        [JsonPropertyName("idNotificacion")]
        public Guid IdNotificacion { get; set; }

        /// <summary>
        /// ID del usuario destinatario
        /// </summary>
        [JsonPropertyName("usuariold")]
        public int UsuarioId { get; set; }

        /// <summary>
        /// Nombre del usuario destinatario
        /// </summary>
        [JsonPropertyName("usuarioNombre")]
        public string UsuarioNombre { get; set; } = string.Empty;

        /// <summary>
        /// Fecha de creación del destinatario
        /// </summary>
        [JsonPropertyName("fechaCreacion")]
        public DateTime FechaCreacion { get; set; }

        /// <summary>
        /// Indica si el destinatario está activo
        /// </summary>
        [JsonPropertyName("esActiva")]
        public bool EsActiva { get; set; }

        /// <summary>
        /// Indica si el destinatario ha leído la notificación
        /// </summary>
        [JsonPropertyName("leida")]
        public bool Leida { get; set; }

        /// <summary>
        /// Fecha en que el destinatario leyó la notificación
        /// </summary>
        [JsonPropertyName("fechaLeida")]
        public DateTime? FechaLeida { get; set; }
    }
}

