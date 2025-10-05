using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SGA_Api.Models.Notificaciones
{
    /// <summary>
    /// Modelo principal para las notificaciones del sistema
    /// </summary>
    [Table("Notificaciones")]
    public class Notificacion
    {
        /// <summary>
        /// Identificador único de la notificación
        /// </summary>
        [Key]
        public Guid IdNotificacion { get; set; }

        /// <summary>
        /// Código de la empresa (por defecto 1)
        /// </summary>
        [Required]
        public short CodigoEmpresa { get; set; } = 1;

        /// <summary>
        /// Tipo de notificación: TRASPASO, INVENTARIO, ORDEN_TRASPASO, CONTEO, AVISO_GENERAL
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string TipoNotificacion { get; set; } = string.Empty;

        /// <summary>
        /// ID del proceso relacionado (traspaso, inventario, etc.)
        /// </summary>
        public Guid? ProcesoId { get; set; }

        /// <summary>
        /// Título de la notificación
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string Titulo { get; set; } = string.Empty;

        /// <summary>
        /// Mensaje detallado de la notificación
        /// </summary>
        [Required]
        [MaxLength(500)]
        public string Mensaje { get; set; } = string.Empty;

        /// <summary>
        /// Estado anterior del proceso (opcional)
        /// </summary>
        [MaxLength(20)]
        public string? EstadoAnterior { get; set; }

        /// <summary>
        /// Estado actual del proceso (opcional)
        /// </summary>
        [MaxLength(20)]
        public string? EstadoActual { get; set; }

        /// <summary>
        /// Fecha de creación de la notificación
        /// </summary>
        [Required]
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Indica si la notificación está activa
        /// </summary>
        [Required]
        public bool EsActiva { get; set; } = true;

        /// <summary>
        /// Indica si la notificación es para un grupo de usuarios
        /// </summary>
        [Required]
        public bool EsGrupal { get; set; } = false;

        /// <summary>
        /// Grupo destino para notificaciones grupales
        /// </summary>
        [MaxLength(50)]
        public string? GrupoDestino { get; set; }

        /// <summary>
        /// Comentario adicional de la notificación
        /// </summary>
        [MaxLength(500)]
        public string? Comentario { get; set; }

        // Propiedades de navegación
        /// <summary>
        /// Lista de destinatarios de la notificación
        /// </summary>
        public virtual ICollection<NotificacionDestinatario> Destinatarios { get; set; } = new List<NotificacionDestinatario>();

        /// <summary>
        /// Lista de lecturas de la notificación
        /// </summary>
        public virtual ICollection<NotificacionLectura> Lecturas { get; set; } = new List<NotificacionLectura>();
    }
}
