namespace SGA_Api.Models.Impresion.ImpUbiMultiple
{
	public class UbicacionLoteItemDto
	{
		public string CodigoAlmacen { get; set; } = null!;
		public string CodigoUbicacion { get; set; } = null!;
		public int? Altura { get; set; }
		public int? Estanteria { get; set; }
		public int? Pasillo { get; set; }
		public int? Posicion { get; set; }
	}
}
