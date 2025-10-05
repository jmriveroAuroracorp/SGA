using System;
using System.ComponentModel.DataAnnotations;

namespace SGA_Desktop.Models
{
    /// <summary>
    /// DTO para marcar una notificación como leída (coincide con la API)
    /// </summary>
    public class MarcarLeidaApiDto
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

