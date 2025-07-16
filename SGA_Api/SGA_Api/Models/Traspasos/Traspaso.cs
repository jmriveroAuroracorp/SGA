using SGA_Api.Models.Palet;

namespace SGA_Api.Models.Traspasos
{
	public class Traspaso
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
		public string? UbicacionOrigen{ get; set; }

		public string? CodigoPalet { get; set; }
		public string? CodigoArticulo { get; set; }
		public decimal? Cantidad { get; set; }
		public string? TipoTraspaso { get; set; }
		public Palet.Palet Palet { get; set; }

		public DateTime? FechaCaducidad { get; set; }
		public string Partida { get; set; }

		public Guid MovPosicionOrigen { get; set; } = Guid.Empty;
		public Guid MovPosicionDestino { get; set; } = Guid.Empty;

		// Opcional: navegación a Palet y Estado
		// public Palet Palet { get; set; }
		// public EstadoTraspaso Estado { get; set; }
	}

}
