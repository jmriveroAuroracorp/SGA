using System;
using System.Collections.Generic;
using SGA_Api.Models.Palet;

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
		public string UsuarioInicioNombre { get; set; } = "";

		public Guid PaletId { get; set; }

		public DateTime? FechaFinalizacion { get; set; }
		public int? UsuarioFinalizacionId { get; set; }
		public string UsuarioFinalizacionNombre { get; set; } = "";
		public string? UbicacionDestino { get; set; }
		public string? UbicacionOrigen { get; set; }

		public string? CodigoPalet { get; set; }
		public string? CodigoArticulo { get; set; }
		public string? DescripcionArticulo { get; set; }
		public string? TipoTraspaso { get; set; }
		public decimal? Cantidad { get; set; }

		
		// Líneas del palet
		public List<LineaPaletDto> LineasPalet { get; set; } = new List<LineaPaletDto>();
	}
}
