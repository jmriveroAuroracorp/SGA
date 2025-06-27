using System.Text.Json.Serialization;

namespace SGA_Api.Models.Ubicacion
{
	public class CrearUbicacionDetalladaDto
	{
		[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
		public short CodigoEmpresa { get; set; }
		public string CodigoAlmacen { get; set; }
		public string CodigoUbicacion { get; set; }
		public string? DescripcionUbicacion { get; set; }
		public int? Pasillo { get; set; }
		public int? Estanteria { get; set; }
		public int? Altura { get; set; }
		public int? Posicion { get; set; }
		public int? TemperaturaMin { get; set; }
		public int? TemperaturaMax { get; set; }
		public decimal? Peso { get; set; }
		public decimal? Alto { get; set; }
		public decimal? DimensionX { get; set; }
		public decimal? DimensionY { get; set; }
		public decimal? DimensionZ { get; set; }
		public decimal? Angulo { get; set; }
		public string? TipoPaletPermitido { get; set; }
		public bool? Habilitada { get; set; }
		public int? Orden { get; set;}
		[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
		public short? TipoUbicacionId { get; set; }
		/// <summary>Códigos de alérgenos que estarán permitidos en esta ubicación.</summary>
		public List<short> AlergenosPermitidos { get; set; } = new();

		/// <summary>Marca para indicar que el código está duplicado en la generación.</summary>
		public bool IsDuplicate { get; set; }

	}
}

