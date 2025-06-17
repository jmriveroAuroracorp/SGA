using System;
using System.Text.Json.Serialization;

namespace SGA_Desktop.Models
{
	/// <summary>
	/// DTO para recibir los resultados de la consulta de stock.
	/// </summary>
	public class StockDto
	{
		[JsonPropertyName("codigoEmpresa")]
		public int CodigoEmpresa { get; set; }

		[JsonPropertyName("codigoArticulo")]
		public string CodigoArticulo { get; set; } = string.Empty;

		// Nuevo: código de almacén para filtrar
		[JsonPropertyName("codigoAlmacen")]
		public string CodigoAlmacen { get; set; } = string.Empty;

		[JsonPropertyName("almacen")]
		public string Almacen { get; set; } = string.Empty;

		[JsonPropertyName("ubicacion")]
		public string Ubicacion { get; set; } = string.Empty;

		[JsonPropertyName("partida")]
		public string Partida { get; set; } = string.Empty;

		[JsonPropertyName("fechaCaducidad")]
		public DateTime? FechaCaducidad { get; set; }

		[JsonPropertyName("unidadSaldo")]
		public decimal UnidadSaldo { get; set; }

		[JsonPropertyName("descripcionArticulo")]
		public string? DescripcionArticulo { get; set; }

		[JsonPropertyName("codigoAlternativo")]
		public string CodigoAlternativo { get; set; } = string.Empty;

		// JSON debe venir como "alergenos" o "vNEWAlergenos" según tu API
		[JsonPropertyName("alergenos")]
		public string Alergenos { get; set; } = string.Empty;
	}
}
