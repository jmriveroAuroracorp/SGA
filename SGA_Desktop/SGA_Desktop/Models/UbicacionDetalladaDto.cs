using Newtonsoft.Json;
using SGA_Desktop.Models;
using System.Collections.ObjectModel;

public class UbicacionDetalladaDto
{
	public UbicacionDetalladaDto()
	{
		AlergenosPresentesList = new ObservableCollection<AlergenoDto>();
		AlergenosPermitidosList = new ObservableCollection<AlergenoDto>();
	}

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

	// Estas dos reciben los datos reales que manda tu API:
	[JsonProperty("tipoUbicacionId")]
	public short? TipoUbicacionId { get; set; }

	[JsonProperty("tipoUbicacionDescripcion")]
	public string TipoUbicacionDescripcion { get; set; } = "";
	//public string TipoUbicacion
	//	=> TipoUbicacionDescripcion;


	[JsonProperty("habilitada")]
	public bool Habilitada { get; set; }

	[JsonProperty("alergenosPermitidos")]
	public string AlergenosPermitidos { get; set; } = "";

	[JsonProperty("alergenosPresentes")]
	public string AlergenosPresentes { get; set; } = "";

	[JsonProperty("riesgoContaminacion")]
	public bool RiesgoContaminacion { get; set; }

	// Colecciones para binding de chips
	public ObservableCollection<AlergenoDto> AlergenosPermitidosList { get; }
		= new ObservableCollection<AlergenoDto>();
	public ObservableCollection<AlergenoDto> AlergenosPresentesList { get; }
		= new ObservableCollection<AlergenoDto>();
}
