using System.ComponentModel.DataAnnotations;

namespace SGA_Api.Models.Notificaciones
{
    /// <summary>
    /// DTO para crear una nueva notificación
    /// </summary>
    public class CrearNotificacionDto
    {
        /// <summary>
        /// Código de la empresa (opcional, por defecto 1)
        /// </summary>
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
        /// Indica si la notificación es para un grupo de usuarios
        /// </summary>
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

        /// <summary>
        /// Lista de IDs de usuarios destinatarios (para notificaciones individuales)
        /// </summary>
        public List<int> UsuarioIds { get; set; } = new List<int>();
    }
}
