using System.ComponentModel.DataAnnotations;

namespace SGA_Api.Models.Notificaciones
{
    /// <summary>
    /// DTO para marcar una notificación como leída
    /// </summary>
    public class MarcarLeidaDto
    {
        /// <summary>
        /// ID de la notificación a marcar como leída
        /// </summary>
        [Required]
        public Guid IdNotificacion { get; set; }

        /// <summary>
        /// ID del usuario que marca como leída la notificación
        /// </summary>
        [Required]
        public int UsuarioId { get; set; }

        /// <summary>
        /// Fecha de lectura (opcional, por defecto la fecha actual)
        /// </summary>
        public DateTime? FechaLeida { get; set; }
    }
}
