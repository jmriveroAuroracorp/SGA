using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SGA_Api.Models.Notificaciones
{
    /// <summary>
    /// Modelo para la tabla MRH_Notificaciones en AURORA
    /// </summary>
    [Table("MRH_Notificaciones")]
    public class MrhNotificacion
    {
        /// <summary>
        /// Código de la empresa
        /// </summary>
        [Key]
        [Column("CodigoEmpresa")]
        public short CodigoEmpresa { get; set; }

        /// <summary>
        /// MovPosicion (ID del traspaso)
        /// </summary>
        [Key]
        [Column("MovPosicion")]
        public Guid MovPosicion { get; set; }

        /// <summary>
        /// Origen de la notificación
        /// </summary>
        [Column("MRH_OrigenNotificacion")]
        [MaxLength(100)]
        public string? OrigenNotificacion { get; set; }

        /// <summary>
        /// Campo interno
        /// </summary>
        [Column("MRH_Interno")]
        public int? Interno { get; set; }

        /// <summary>
        /// Fecha de registro
        /// </summary>
        [Column("FechaRegistro")]
        public DateTime? FechaRegistro { get; set; }

        /// <summary>
        /// Fecha confirmada de envío
        /// </summary>
        [Column("FechaConfirmadaEnvio")]
        public DateTime? FechaConfirmadaEnvio { get; set; }

        /// <summary>
        /// Indica si se envía email
        /// </summary>
        [Column("EnviaEmail")]
        public short? EnviaEmail { get; set; }

        /// <summary>
        /// Indica si se envía por app
        /// </summary>
        [Column("EnviaApp")]
        public short? EnviaApp { get; set; }

        /// <summary>
        /// Indica si fue leído
        /// </summary>
        [Column("Leido")]
        public short? Leido { get; set; }

        /// <summary>
        /// Email destinatario
        /// </summary>
        [Column("Email")]
        [MaxLength(200)]
        public string? Email { get; set; }

        /// <summary>
        /// Nombre del destinatario
        /// </summary>
        [Column("Nombre")]
        [MaxLength(200)]
        public string? Nombre { get; set; }

        /// <summary>
        /// Asunto de la notificación
        /// </summary>
        [Column("Asunto")]
        [MaxLength(500)]
        public string? Asunto { get; set; }

        /// <summary>
        /// Mensaje de la notificación
        /// </summary>
        [Column("Mensaje")]
        public string? Mensaje { get; set; }

        /// <summary>
        /// Error en el envío
        /// </summary>
        [Column("ErrorEnvio")]
        [MaxLength(500)]
        public string? ErrorEnvio { get; set; }

        /// <summary>
        /// Email del emisor
        /// </summary>
        [Column("EmailEmisor")]
        [MaxLength(200)]
        public string? EmailEmisor { get; set; }

        /// <summary>
        /// SMTP del emisor
        /// </summary>
        [Column("SmtpEmisor")]
        [MaxLength(200)]
        public string? SmtpEmisor { get; set; }

        /// <summary>
        /// Contraseña del emisor
        /// </summary>
        [Column("PassEmisor")]
        [MaxLength(200)]
        public string? PassEmisor { get; set; }

        /// <summary>
        /// Firma del emisor
        /// </summary>
        [Column("FirmaEmisor")]
        public string? FirmaEmisor { get; set; }

        /// <summary>
        /// ID de Telegram
        /// </summary>
        [Column("TelegramID")]
        [MaxLength(100)]
        public string? TelegramID { get; set; }

        /// <summary>
        /// Fecha confirmada de envío a Teams
        /// </summary>
        [Column("FechaConfirmadaEnvioT")]
        public DateTime? FechaConfirmadaEnvioT { get; set; }

        /// <summary>
        /// URL del webhook de Teams
        /// </summary>
        [Column("CanalTeams")]
        [MaxLength(500)]
        public string? CanalTeams { get; set; }
    }
}

