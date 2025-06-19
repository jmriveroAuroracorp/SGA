using Newtonsoft.Json;

namespace SGA_Desktop.Models
{
	public class UbicacionDetalladaDto
	{
		[JsonProperty("codigoEmpresa")]
		public short CodigoEmpresa { get; set; }

		[JsonProperty("codigoAlmacen")]
		public string CodigoAlmacen { get; set; } = "";

		[JsonProperty("ubicacion")]
		public string Ubicacion { get; set; } = "";

		[JsonProperty("descripcionUbicacion")]
		public string DescripcionUbicacion { get; set; } = "";

		[JsonProperty("pasillo")]
		public int? Pasillo { get; set; }

		[JsonProperty("estanteria")]
		public int? Estanteria { get; set; }

		[JsonProperty("altura")]
		public int? Altura { get; set; }

		[JsonProperty("posicion")]
		public int? Posicion { get; set; }

		[JsonProperty("temperaturaMin")]
		public int? TemperaturaMin { get; set; }

		[JsonProperty("temperaturaMax")]
		public int? TemperaturaMax { get; set; }

		[JsonProperty("tipoPaletPermitido")]
		public string TipoPaletPermitido { get; set; } = "";

		[JsonProperty("tipoUbicacion")]
		public string TipoUbicacion { get; set; } = "";

		[JsonProperty("habilitada")]
		public bool Habilitada { get; set; }

		[JsonProperty("alergenosPermitidos")]
		public string AlergenosPermitidos { get; set; } = "";

		[JsonProperty("alergenosPresentes")]
		public string AlergenosPresentes { get; set; } = "";

		[JsonProperty("riesgoContaminacion")]
		public bool RiesgoContaminacion { get; set; }
	}
}
