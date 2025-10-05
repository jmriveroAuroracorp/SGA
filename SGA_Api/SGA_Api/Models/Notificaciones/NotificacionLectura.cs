using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SGA_Api.Models.Notificaciones
{
    /// <summary>
    /// Modelo para las lecturas de las notificaciones por parte de los usuarios
    /// </summary>
    [Table("NotificacionesLecturas")]
    public class NotificacionLectura
    {
        /// <summary>
        /// Identificador único de la lectura
        /// </summary>
        [Key]
        public Guid IdLectura { get; set; }

        /// <summary>
        /// ID de la notificación leída
        /// </summary>
        [Required]
        public Guid IdNotificacion { get; set; }

        /// <summary>
        /// ID del usuario que leyó la notificación
        /// </summary>
        [Required]
        public int UsuarioId { get; set; }

        /// <summary>
        /// Fecha y hora en que se leyó la notificación
        /// </summary>
        [Required]
        public DateTime FechaLeida { get; set; } = DateTime.UtcNow;

        // Propiedades de navegación
        /// <summary>
        /// Notificación leída
        /// </summary>
        [ForeignKey(nameof(IdNotificacion))]
        public virtual Notificacion Notificacion { get; set; } = null!;

        /// <summary>
        /// Usuario que leyó la notificación
        /// </summary>
        [ForeignKey(nameof(UsuarioId))]
        public virtual UsuarioConf.Usuario Usuario { get; set; } = null!;
    }
}
