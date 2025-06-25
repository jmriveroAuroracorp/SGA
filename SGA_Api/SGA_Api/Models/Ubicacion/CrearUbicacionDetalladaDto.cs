namespace SGA_Api.Models.Ubicacion
{
	public class CrearUbicacionDetalladaDto
	{
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
		public string? TipoPaletPermitido { get; set; }
		public bool? Habilitada { get; set; }
		public short? TipoUbicacionId { get; set; }
		/// <summary>Códigos de alérgenos que estarán permitidos en esta ubicación.</summary>
		public List<short> AlergenosPermitidos { get; set; } = new();

	}
}

