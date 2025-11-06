using System.ComponentModel.DataAnnotations.Schema;

namespace SGA_Api.Models.Palet
{
	public class PaletLinea
	{
		public Guid Id { get; set; }
		public Guid PaletId { get; set; }
		public short CodigoEmpresa { get; set; }
		public string CodigoArticulo { get; set; } = null!;
		public string? DescripcionArticulo { get; set; }
		/// <summary>
		/// 🔷 ACTUALIZADO: Precisión de 6 decimales para preservar valores exactos
		/// </summary>
		[Column(TypeName = "decimal(18,6)")]
		public decimal Cantidad { get; set; }
		public string? UnidadMedida { get; set; }
		public string? Lote { get; set; }
		public DateTime? FechaCaducidad { get; set; }
		public string CodigoAlmacen { get; set; } = null!;
		public string Ubicacion { get; set; } = null!;
		public int UsuarioId { get; set; }
		public DateTime FechaAgregado { get; set; }
		public string? Observaciones { get; set; }
		public Guid? TraspasoId { get; set; }

		//navegacion
		public Palet? Palet { get; set; } 
	}
}
