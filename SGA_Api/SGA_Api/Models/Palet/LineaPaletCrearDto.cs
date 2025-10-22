namespace SGA_Api.Models.Palet
{
	public class LineaPaletCrearDto
	{
		public short CodigoEmpresa { get; set; }
		public string CodigoArticulo { get; set; } = null!;
		public string? DescripcionArticulo { get; set; }
		public decimal Cantidad { get; set; }
		public string? Lote { get; set; }
		public DateTime? FechaCaducidad { get; set; }
		public string CodigoAlmacen { get; set; } = null!;
		public string Ubicacion { get; set; } = null!;
		public int UsuarioId { get; set; }
		public string? Observaciones { get; set; }
		/// <summary>
		/// Opcional: Palet origen explícito desde el que se desea extraer material.
		/// Si se informa, el backend usará este palet como origen para crear la línea negativa.
		/// </summary>
		public Guid? PaletIdOrigen { get; set; }
	}
}
