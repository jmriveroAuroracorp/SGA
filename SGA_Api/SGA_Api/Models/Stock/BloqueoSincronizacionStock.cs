namespace SGA_Api.Models.Stock
{
	public class BloqueoSincronizacionStock
	{
		public int Id { get; set; }
		public short CodigoEmpresa { get; set; }
		public string CodigoArticulo { get; set; } = null!;
		public string? Partida { get; set; }
		public string CodigoAlmacen { get; set; } = null!;
		public string? Ubicacion { get; set; }
		public decimal StockSage { get; set; }
		public decimal StockStorageControl { get; set; }
		public decimal Diferencia { get; set; }
		public DateTime FechaBloqueo { get; set; }
		public int UsuarioId { get; set; }
		public string? TipoOperacion { get; set; } // "CREAR_TRASPASO_ARTICULO", "ANHADIR_LINEA_PALET", "MOVER_PALET", "CERRAR_PALET", "CERRAR_PALET_MOBILITY"
		public Guid? PaletId { get; set; }
		public string? CodigoPalet { get; set; }
		public string? MensajeError { get; set; }
		public bool Notificado { get; set; }
	}
}
