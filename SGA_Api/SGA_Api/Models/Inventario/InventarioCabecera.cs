using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SGA_Api.Models.Inventario
{
    [Table("InventarioCabecera")]
    public class InventarioCabecera
    {
        [Key]
        public Guid IdInventario { get; set; }

        [Required]
        [StringLength(30)]
        public string CodigoInventario { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "SMALLINT")]
        public int CodigoEmpresa { get; set; }

        [Required]
        [StringLength(10)]
        public string CodigoAlmacen { get; set; } = string.Empty;

        [StringLength(50)]
        public string? RangoUbicaciones { get; set; }

        [Required]
        [StringLength(10)]
        public string TipoInventario { get; set; } = "TOTAL";

        [Column(TypeName = "NVARCHAR(500)")]
        public string? Comentarios { get; set; }

        [Required]
        [StringLength(20)]
        public string Estado { get; set; } = "ABIERTO";

        [Required]
        public int UsuarioCreacionId { get; set; }

        // Propiedad calculada para el nombre del usuario (no se mapea a la BD)
        [NotMapped]
        public string? UsuarioCreacionNombre { get; set; }

        // Usuario que procesa el inventario
        public int? UsuarioProcesamientoId { get; set; }

        // Propiedad calculada para el nombre del usuario de procesamiento (no se mapea a la BD)
        [NotMapped]
        public string? UsuarioProcesamientoNombre { get; set; }

        [Required]
        [Column(TypeName = "DATETIME")]
        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        [Column(TypeName = "DATETIME")]
        public DateTime? FechaCierre { get; set; }

        // Navigation properties
        public virtual ICollection<InventarioLineasTemp> LineasTemp { get; set; } = new List<InventarioLineasTemp>();
        public virtual ICollection<InventarioLineas> Lineas { get; set; } = new List<InventarioLineas>();
        public virtual ICollection<InventarioAjustes> Ajustes { get; set; } = new List<InventarioAjustes>();
    }
} 