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

		// NUEVO: Campo para preservar el almacén de cada línea individual
		[StringLength(30)]
		public string? CodigoAlmacen { get; set; }

		[Required]
		[Column(TypeName = "decimal(18,4)")]
		public decimal StockTeorico { get; set; } = 0;

		[Column(TypeName = "decimal(18,4)")]
		public decimal? StockContado { get; set; }

		[Required]
		[StringLength(20)]
		public string Estado { get; set; } = "PENDIENTE";

		public int? UsuarioValidacionId { get; set; }

		public DateTime? FechaValidacion { get; set; }

		[StringLength(500)]
		public string? Observaciones { get; set; }

		[StringLength(50)]
		public string? Partida { get; set; }

		public DateTime? FechaCaducidad { get; set; }

		[Required]
		[Column(TypeName = "decimal(18,4)")]
		public decimal StockActual { get; set; } = 0;

		/// <summary>
		/// Ajuste final calculado: StockContado - StockActual
		/// Representa la diferencia entre lo que contó el usuario y lo que hay actualmente en el sistema
		/// </summary>
		[Column(TypeName = "decimal(18,4)")]
		public decimal? AjusteFinal { get; set; }

		// Navigation property
		[ForeignKey("IdInventario")]
		public virtual InventarioCabecera Inventario { get; set; } = null!;
	}
}