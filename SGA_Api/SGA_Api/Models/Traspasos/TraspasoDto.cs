namespace SGA_Api.Models.Traspasos
{
	public class TraspasoDto
	{
		public Guid Id { get; set; }

		public string AlmacenOrigen { get; set; }
		public string AlmacenDestino { get; set; }

		public string CodigoEstado { get; set; }

		public DateTime FechaInicio { get; set; }
		public int UsuarioInicioId { get; set; }

		public Guid PaletId { get; set; }

		public DateTime? FechaFinalizacion { get; set; }
		public int? UsuarioFinalizacionId { get; set; }
		public string? UbicacionDestino { get; set; }
		public string? UbicacionOrigen { get; set; }

		public string? CodigoPalet { get; set; }
		public string? CodigoArticulo { get; set; }
		public string? TipoTraspaso { get; set; }
	}


}
