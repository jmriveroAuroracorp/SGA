using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SGA_Api.Models.Traspasos;

namespace SGA_Api.Models.Notificaciones
{
    /// <summary>
    /// Modelo para la cola de notificaciones Teams en AURORA_SGA
    /// </summary>
    [Table("NotificacionesTeamsCola")]
    public class NotificacionTeamsCola
    {
        /// <summary>
        /// Identificador único del registro en la cola
        /// </summary>
        [Key]
        public Guid Id { get; set; }

        /// <summary>
        /// ID del traspaso relacionado
        /// </summary>
        [Required]
        public Guid TraspasoId { get; set; }

        /// <summary>
        /// Estado del procesamiento: Pendiente, Enviado, Error
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string Estado { get; set; } = "Pendiente";

        /// <summary>
        /// Número de intentos de procesamiento
        /// </summary>
        [Required]
        public int Intentos { get; set; } = 0;

        /// <summary>
        /// Fecha de creación del registro en la cola
        /// </summary>
        [Required]
        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        /// <summary>
        /// Fecha en que se procesó exitosamente
        /// </summary>
        public DateTime? FechaProcesado { get; set; }

        /// <summary>
        /// Mensaje de error si falló el procesamiento
        /// </summary>
        [MaxLength(500)]
        public string? ErrorMensaje { get; set; }

        /// <summary>
        /// Mensaje de error del traspaso (EstadoErp o Comentario)
        /// </summary>
        public string? MensajeError { get; set; }

        // Propiedad de navegación
        /// <summary>
        /// Traspaso relacionado
        /// </summary>
        [ForeignKey("TraspasoId")]
        public virtual Traspaso? Traspaso { get; set; }
    }
}

