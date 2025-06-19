namespace SGA_Api.Models.Ubicacion
{
	public class UbicacionDetallada
	{
		public short CodigoEmpresa { get; set; }
		public string CodigoAlmacen { get; set; }
		public string Ubicacion { get; set; }
		public string DescripcionUbicacion { get; set; }

		public int? Pasillo { get; set; }
		public int? Estanteria { get; set; }
		public int? Altura { get; set; }
		public int? Posicion { get; set; }

		public int? TemperaturaMin { get; set; }
		public int? TemperaturaMax { get; set; }
		public string TipoPaletPermitido { get; set; }
		public bool Habilitada { get; set; }
		public string TipoUbicacion { get; set; }

		public string AlergenosPermitidos { get; set; }
		public string AlergenosPresentes { get; set; }
		public bool RiesgoContaminacion { get; set; }
	}

}
