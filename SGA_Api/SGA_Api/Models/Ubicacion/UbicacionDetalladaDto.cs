using System.Text.Json.Serialization;

namespace SGA_Api.Models.Ubicacion
{
	public class UbicacionDetalladaDto
	{
		[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
		public short CodigoEmpresa { get; set; }
		public string CodigoAlmacen { get; set; }
		public string CodigoUbicacion { get; set; }
		public string DescripcionUbicacion { get; set; }
		public int? Pasillo { get; set; }
		public int? Estanteria { get; set; }
		public int? Altura { get; set; }
		public int? Posicion { get; set; }
		public int? Orden { get; set; }
		public int? TemperaturaMin { get; set; }
		public int? TemperaturaMax { get; set; }
		public string? TipoPaletPermitido { get; set; }
		public bool Habilitada { get; set; }
		public decimal? Peso { get; set; }
		public decimal? DimensionX { get; set; }
		public decimal? DimensionY { get; set; }
		public decimal? DimensionZ { get; set; }
		public decimal? Angulo { get; set; }
		[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
		public short? TipoUbicacionId { get; set; }
		public string TipoUbicacionDescripcion { get; set; }
		public List<string> AlergenosPermitidos { get; set; } = new();
		public List<string> AlergenosPresentes { get; set; } = new();

	}
}
