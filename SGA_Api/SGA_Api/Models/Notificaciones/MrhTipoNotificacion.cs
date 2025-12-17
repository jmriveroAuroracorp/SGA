using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SGA_Api.Models.Notificaciones
{
    /// <summary>
    /// Modelo para la tabla MRH_TiposNotificacione en AURORA
    /// Nota: Los datos de Email y CanalTeams se obtienen de MRH_NotificacionesEmail mediante JOIN
    /// </summary>
    [Table("MRH_TiposNotificacione")]
    public class MrhTipoNotificacion
    {
        /// <summary>
        /// Código de la empresa (obtenido de MRH_NotificacionesEmail)
        /// </summary>
        [Column("CodigoEmpresa")]
        public short CodigoEmpresa { get; set; }

        /// <summary>
        /// Tipo de notificación (94, 95, 96, 97, 98)
        /// </summary>
        [Key]
        [Column("MRH_TipoNotificacion")]
        public short TipoNotificacion { get; set; }

        /// <summary>
        /// Email configurado para este tipo
        /// </summary>
        [Column("Email")]
        [MaxLength(200)]
        public string? Email { get; set; }

        /// <summary>
        /// Teléfono configurado
        /// </summary>
        [Column("Telefono")]
        [MaxLength(50)]
        public string? Telefono { get; set; }

        /// <summary>
        /// ID de Telegram configurado
        /// </summary>
        [Column("TelegramID")]
        [MaxLength(100)]
        public string? TelegramID { get; set; }

        /// <summary>
        /// Descripción del tipo de notificación
        /// </summary>
        [Column("Descripcion")]
        [MaxLength(200)]
        public string? Descripcion { get; set; }

        /// <summary>
        /// URL del webhook de Teams
        /// </summary>
        [Column("CanalTeams")]
        [MaxLength(500)]
        public string? CanalTeams { get; set; }

        /// <summary>
        /// Departamento asociado
        /// </summary>
        [Column("Departamento")]
        [MaxLength(100)]
        public string? Departamento { get; set; }
    }
}

