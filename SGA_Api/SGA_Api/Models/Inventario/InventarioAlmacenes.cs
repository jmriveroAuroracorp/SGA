using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SGA_Api.Models.Inventario
{
    [Table("InventarioAlmacenes")]
    public class InventarioAlmacenes
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid IdInventario { get; set; }

        [Required]
        [StringLength(10)]
        public string CodigoAlmacen { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "SMALLINT")]
        public short CodigoEmpresa { get; set; }

        [Required]
        [Column(TypeName = "DATETIME")]
        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        // Navigation property
        [ForeignKey("IdInventario")]
        public virtual InventarioCabecera? Inventario { get; set; }
    }
} 