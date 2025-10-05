using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SGA_Api.Models.Notificaciones
{
    /// <summary>
    /// Modelo para los destinatarios de las notificaciones
    /// </summary>
    [Table("NotificacionesDestinatarios")]
    public class NotificacionDestinatario
    {
        /// <summary>
        /// Identificador único del destinatario
        /// </summary>
        [Key]
        public Guid IdDestinatario { get; set; }

        /// <summary>
        /// ID de la notificación relacionada
        /// </summary>
        [Required]
        public Guid IdNotificacion { get; set; }

        /// <summary>
        /// ID del usuario destinatario
        /// </summary>
        [Required]
        public int UsuarioId { get; set; }

        /// <summary>
        /// Fecha de creación del destinatario
        /// </summary>
        [Required]
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Indica si el destinatario está activo
        /// </summary>
        [Required]
        public bool EsActiva { get; set; } = true;

        // Propiedades de navegación
        /// <summary>
        /// Notificación relacionada
        /// </summary>
        [ForeignKey(nameof(IdNotificacion))]
        public virtual Notificacion Notificacion { get; set; } = null!;

        /// <summary>
        /// Usuario destinatario
        /// </summary>
        [ForeignKey(nameof(UsuarioId))]
        public virtual UsuarioConf.Usuario Usuario { get; set; } = null!;
    }
}
