using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SGA_Api.Models.Inventario
{
    [Table("OrdenConversionLineas")]
    public class OrdenConversionLineas
    {
        [Key]
        [Column("IdLinea")]
        public Guid IdLinea { get; set; } = Guid.NewGuid();

        [Required]
        public Guid IdOrdenConversion { get; set; }

        [Required]
        public int NumeroLinea { get; set; }

        [Required]
        [StringLength(20)]
        public string TipoCambio { get; set; } = string.Empty; // "CAMBIO_CODIGO" o "AMPLIACION"

        [Required]
        [StringLength(10)]
        public string CodigoAlmacen { get; set; } = string.Empty;

        [Required]
        [StringLength(30)]
        public string Ubicacion { get; set; } = string.Empty;

        public Guid? PaletId { get; set; }

        [StringLength(50)]
        public string? Partida { get; set; }

        public DateTime? FechaCaducidad { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,6)")]
        public decimal Cantidad { get; set; }

        /// <summary>
        /// Cantidad en unidades de destino para esta línea (ej. 2 botes → 120 pastillas).
        /// </summary>
        [Column(TypeName = "decimal(18,6)")]
        public decimal CantidadFinal { get; set; }

        [Required]
        public int UsuarioEjecucionId { get; set; }

        [Required]
        public DateTime FechaEjecucion { get; set; } = DateTime.Now;

        // Navigation property
        [ForeignKey("IdOrdenConversion")]
        public virtual OrdenConversionCabecera OrdenConversion { get; set; } = null!;
    }
}
