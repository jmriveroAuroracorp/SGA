using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SGA_Desktop.Models
{
	public class PaletDetalleDto
	{
		[JsonPropertyName("paletId")]
		public Guid PaletId { get; set; }

		[JsonPropertyName("codigoPalet")]
		public string CodigoPalet { get; set; } = string.Empty;

		[JsonPropertyName("estadoPalet")]
		public string EstadoPalet { get; set; } = string.Empty;

		[JsonPropertyName("cantidad")]
		public decimal Cantidad { get; set; }

		[JsonPropertyName("ubicacion")]
		public string? Ubicacion { get; set; }

		[JsonPropertyName("partida")]
		public string? Partida { get; set; }

		[JsonPropertyName("fechaApertura")]
		public DateTime? FechaApertura { get; set; }

		[JsonPropertyName("fechaCierre")]
		public DateTime? FechaCierre { get; set; }
	}
}
