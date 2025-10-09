using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SGA_Api.Models.Calidad
{
    [Table("BloqueosCalidad")]
    public class BloqueoCalidad
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public short CodigoEmpresa { get; set; }

        [Required]
        [StringLength(30)]
        public string CodigoArticulo { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string LotePartida { get; set; } = string.Empty;

        [Required]
        [StringLength(10)]
        public string CodigoAlmacen { get; set; } = string.Empty;

        [StringLength(30)]
        public string? Ubicacion { get; set; }

        [Required]
        public bool Bloqueado { get; set; } = true;

        [Required]
        public int UsuarioBloqueoId { get; set; }

        [Required]
        public DateTime FechaBloqueo { get; set; } = DateTime.Now;

        [Required]
        [StringLength(500)]
        public string ComentarioBloqueo { get; set; } = string.Empty;

        public int? UsuarioDesbloqueoId { get; set; }

        public DateTime? FechaDesbloqueo { get; set; }

        [StringLength(500)]
        public string? ComentarioDesbloqueo { get; set; }

        [Required]
        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        [Required]
        public DateTime FechaModificacion { get; set; } = DateTime.Now;
    }
}
