using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using SGA_Desktop.Models;
using System.Collections.ObjectModel;

public partial class UbicacionDetalladaDto : ObservableObject
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

	[JsonProperty("tipoUbicacionId")]
	public short? TipoUbicacionId { get; set; }

	[JsonProperty("tipoUbicacionDescripcion")]
	public string TipoUbicacionDescripcion { get; set; } = "";

	[JsonProperty("orden")]
	public int? Orden { get; set; }

	[JsonProperty("habilitada")]
	public bool Habilitada { get; set; }

	[JsonProperty("alergenosPermitidos")]
	public string AlergenosPermitidos { get; set; } = "";

	[JsonProperty("alergenosPresentes")]
	public string AlergenosPresentes { get; set; } = "";

	[JsonProperty("riesgoContaminacion")]
	public bool RiesgoContaminacion { get; set; }

	[JsonProperty("peso")]
	public decimal? Peso { get; set; }
	[JsonProperty("alto")]
	public decimal? Alto { get; set; }
	[JsonProperty("dimensionx")]
	public decimal? DimensionX { get; set; }
	[JsonProperty("dimensiony")]
	public decimal? DimensionY { get; set; }
	[JsonProperty("dimensionz")]
	public decimal? DimensionZ { get; set; }
	[JsonProperty("angulo")]
	public decimal? Angulo { get; set; }

	public ObservableCollection<AlergenoDto> AlergenosPermitidosList { get; }
	public ObservableCollection<AlergenoDto> AlergenosPresentesList { get; }

	public string TextoMostrado
		=> string.IsNullOrWhiteSpace(Ubicacion) ? "SIN UBICACIÓN" : Ubicacion;

	[ObservableProperty]
	private bool isMarcada;
}
