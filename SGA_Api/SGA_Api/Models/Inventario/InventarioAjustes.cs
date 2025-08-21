using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SGA_Api.Models.Inventario
{
    [Table("InventarioAjustes")]
    public class InventarioAjustes
    {
        [Key]
        public Guid IdAjuste { get; set; }

        [Required]
        public Guid IdInventario { get; set; }

        [Required]
        [StringLength(30)]
        public string CodigoArticulo { get; set; } = string.Empty;

        [Required]
        [StringLength(30)]
        public string CodigoUbicacion { get; set; } = string.Empty;

        [Required]
        public decimal Diferencia { get; set; }

        [Required]
        [StringLength(10)]
        public string TipoAjuste { get; set; } = string.Empty;

        [Required]
        public int UsuarioId { get; set; }

        [Required]
        public DateTime Fecha { get; set; } = DateTime.Now;

        // Navigation property
        [ForeignKey("IdInventario")]
        public virtual InventarioCabecera Inventario { get; set; } = null!;
    }
} 