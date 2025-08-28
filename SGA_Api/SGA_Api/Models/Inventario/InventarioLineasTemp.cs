using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SGA_Api.Models.Inventario
{
    [Table("InventarioLineasTemp")]
    public class InventarioLineasTemp
    {
        [Key]
        public Guid IdTemp { get; set; }

        [Required]
        public Guid IdInventario { get; set; }

        [Required]
        [StringLength(30)]
        public string CodigoArticulo { get; set; } = string.Empty;

        [Required]
        [StringLength(30)]
        public string CodigoUbicacion { get; set; } = string.Empty;

        [StringLength(50)]
        public string? Partida { get; set; }

        public DateTime? FechaCaducidad { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal? CantidadContada { get; set; }

        /// <summary>
        /// Stock actual en la ubicación al momento de crear la línea temporal
        /// </summary>
        [Column(TypeName = "decimal(18,4)")]
        public decimal StockActual { get; set; } = 0.0000m;

        [Required]
        public int UsuarioConteoId { get; set; }

        [Required]
        public DateTime FechaConteo { get; set; } = DateTime.Now;

        [StringLength(500)]
        public string? Observaciones { get; set; }

        [Required]
        public bool Consolidado { get; set; } = false;

        public DateTime? FechaConsolidacion { get; set; }

        public int? UsuarioConsolidacionId { get; set; }



        // Navigation property
        [ForeignKey("IdInventario")]
        public virtual InventarioCabecera Inventario { get; set; } = null!;
    }
} 