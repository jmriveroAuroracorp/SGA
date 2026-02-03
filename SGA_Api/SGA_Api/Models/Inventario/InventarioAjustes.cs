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

        public Guid? IdInventario { get; set; }

        [Required]
        [StringLength(30)]
        public string CodigoArticulo { get; set; } = string.Empty;

        [Required]
        [StringLength(30)]
        public string CodigoUbicacion { get; set; } = string.Empty;

        /// <summary>
        /// 🔷 ACTUALIZADO: Precisión de 6 decimales para preservar valores exactos
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(18,6)")]
        public decimal Diferencia { get; set; }

        [Required]
        public int UsuarioId { get; set; }

        [Required]
        public DateTime Fecha { get; set; } = DateTime.Now;

        public Guid? IdConteo { get; set; }

        public Guid? IdOrden { get; set; }  
       
        public Guid? IdCambioArticulo { get; set; }

        [Required]
        public short CodigoEmpresa { get; set; }

        [Required]
        [StringLength(10)]
        public string CodigoAlmacen { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string Estado { get; set; } = "PENDIENTE_ERP";

        [StringLength(500)]
        public string? EstadoErp { get; set; }

        public DateTime? FechaCaducidad { get; set; }

        [StringLength(50)]
        public string? Partida { get; set; }

        // Campos para información de palet
        public Guid? PaletId { get; set; }
        
        [StringLength(50)]
        public string? CodigoPalet { get; set; }
        
        [StringLength(50)]
        public string? CodigoGS1 { get; set; }

        // Campo de control para evitar procesamiento duplicado
        public bool ProcesadoPalet { get; set; } = false;

        // Campo de control para evitar notificar el mismo error múltiples veces
        public bool ErrorNotificado { get; set; } = false;

        // Navigation properties
        [ForeignKey("IdInventario")]
        public virtual InventarioCabecera? Inventario { get; set; }

        public virtual CambioArticulo? CambioArticulo { get; set; }
    }
} 