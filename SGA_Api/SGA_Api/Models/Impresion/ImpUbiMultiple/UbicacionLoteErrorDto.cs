namespace SGA_Api.Models.Impresion.ImpUbiMultiple
{
	public class UbicacionLoteErrorDto
	{
		public int Indice { get; set; }
		public string? CodigoAlmacen { get; set; }
		public string? CodigoUbicacion { get; set; }
		public string Mensaje { get; set; } = null!;
	}
}
