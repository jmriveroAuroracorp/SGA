using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SGA_Api.Models.Inventario
{
    [Table("CambioArticulo")]
    public class CambioArticulo
    {
        [Key]
        public Guid IdCambioArticulo { get; set; } = Guid.NewGuid();

        [Required]
        public short CodigoEmpresa { get; set; }

        [Required]
        public int UsuarioId { get; set; }

        /// <summary>
        /// Operario asignado para ejecutar la conversión/ampliación.
        /// Puede ser null cuando el cambio se realiza directamente desde escritorio.
        /// </summary>
        public int? OperarioAsignadoId { get; set; }

        [Required]
        public DateTime Fecha { get; set; } = DateTime.Now;

        // Datos del artículo ORIGEN
        [Required]
        [StringLength(30)]
        public string CodigoArticuloOrigen { get; set; } = string.Empty;

        [Required]
        [StringLength(10)]
        public string CodigoAlmacen { get; set; } = string.Empty;

        [StringLength(30)]
        public string? Ubicacion { get; set; }

        [StringLength(50)]
        public string? PartidaOrigen { get; set; }

        public DateTime? FechaCaducidadOrigen { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,6)")]
        public decimal Cantidad { get; set; }

        public Guid? PaletId { get; set; }

        // Datos del artículo DESTINO
        [StringLength(30)]
        public string? CodigoArticuloDestino { get; set; }

        public DateTime? FechaCaducidadDestino { get; set; }

        [StringLength(50)]
        public string? PartidaDestino { get; set; }

        // Información del cambio
        [Required]
        [StringLength(20)]
        public string TipoCambio { get; set; } = string.Empty; // "CAMBIO_CODIGO" o "AMPLIACION"

        [StringLength(500)]
        public string? Comentario { get; set; }

        [Required]
        [StringLength(20)]
        public string Estado { get; set; } = "PENDIENTE"; // "PENDIENTE", "COMPLETADO", "ERROR"

        // Navigation property
        public virtual ICollection<InventarioAjustes> Ajustes { get; set; } = new List<InventarioAjustes>();
    }
}
