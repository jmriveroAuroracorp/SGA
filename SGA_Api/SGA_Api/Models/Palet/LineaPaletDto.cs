namespace SGA_Api.Models.Palet
{
	public class LineaPaletDto
	{
		public Guid Id { get; set; }
		public Guid PaletId { get; set; }
		public short CodigoEmpresa { get; set; }
		public string CodigoArticulo { get; set; } = null!;
		public string? DescripcionArticulo { get; set; }
		public decimal Cantidad { get; set; }
		public string? UnidadMedida { get; set; }
		public string? Lote { get; set; }
		public DateTime? FechaCaducidad { get; set; }
		public string CodigoAlmacen { get; set; } = null!;
		public string Ubicacion { get; set; } = null!;
		public int UsuarioId { get; set; }
		public DateTime FechaAgregado { get; set; }
		public string? Observaciones { get; set; }

		// 🔷 NUEVO: Indicadores de bloqueo por calidad
		public bool IsBloqueadoCalidad { get; set; }
		public string? MotivoBloqueoCalidad { get; set; }
		public DateTime? FechaBloqueoCalidad { get; set; }
		public string? TipoBloqueoCalidad { get; set; }
		
		// 🔷 NUEVO: TraspasoId para obtener fecha del último traspaso
		public Guid? TraspasoId { get; set; }
	}
}
