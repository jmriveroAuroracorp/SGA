using System.ComponentModel.DataAnnotations;

namespace SGA_Api.Models.Calidad
{
    public class BloquearStockDto
    {
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
        [StringLength(500)]
        public string ComentarioBloqueo { get; set; } = string.Empty;

        [StringLength(20)]
        public string TipoBloqueo { get; set; } = "TOTAL"; // "TOTAL" = bloquea todos los traspasos, "SOLO_PULMON" = solo bloquea a PULMÓN

        [Required]
        public int UsuarioId { get; set; }

        public bool EsBloqueoGlobal { get; set; } = false; // 🔷 NUEVO: Indica si es bloqueo en todas las ubicaciones
    }
}
