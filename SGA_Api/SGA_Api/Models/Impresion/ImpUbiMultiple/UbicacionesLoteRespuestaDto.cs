namespace SGA_Api.Models.Impresion.ImpUbiMultiple
{
	public class UbicacionesLoteRespuestaDto
	{
		public bool Exito { get; set; }
		public int Total { get; set; }
		public int Insertados { get; set; }
		public List<UbicacionLoteErrorDto> Errores { get; set; } = new();
	}
}
