using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SGA_Api.Models.Inventario
{
    [Table("InventarioLineas")]
    public class InventarioLineas
    {
        [Key]
        public Guid IdLinea { get; set; }

        [Required]
        public Guid IdInventario { get; set; }

        [Required]
        [StringLength(30)]
        public string CodigoArticulo { get; set; } = string.Empty;

        [Required]
        [StringLength(30)]
        public string CodigoUbicacion { get; set; } = string.Empty;

        [Required]
        public decimal StockTeorico { get; set; } = 0;

        public decimal? StockContado { get; set; }

        [Required]
        [StringLength(20)]
        public string Estado { get; set; } = "PENDIENTE";

        public int? UsuarioValidacionId { get; set; }

        public DateTime? FechaValidacion { get; set; }

        [StringLength(500)]
        public string? Observaciones { get; set; }

        // Navigation property
        [ForeignKey("IdInventario")]
        public virtual InventarioCabecera Inventario { get; set; } = null!;
    }
} 