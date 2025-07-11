namespace SGA_Api.Models.Stock
{
	public class StockDisponible
	{
		public short CodigoEmpresa { get; set; }
		public string CodigoArticulo { get; set; } = null!;
		public string DescripcionArticulo { get; set; } = null!;
		public string CodigoAlmacen { get; set; } = null!;
		public string Almacen { get; set; } = null!;
		public string Ubicacion { get; set; } = null!;
		public string Partida { get; set; } = null!;
		public DateTime? FechaCaducidad { get; set; }
		public decimal UnidadSaldo { get; set; }
		public decimal Reservado { get; set; }
		public decimal Disponible { get; set; }
	}
}
