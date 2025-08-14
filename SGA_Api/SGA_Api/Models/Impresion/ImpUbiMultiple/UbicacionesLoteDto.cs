namespace SGA_Api.Models.Impresion.ImpUbiMultiple
{
	public class UbicacionesLoteDto
	{
		public int ImpresoraId { get; set; }
		public string Usuario { get; set; } = null!;
		public string Dispositivo { get; set; } = null!;
		public string? RutaEtiqueta { get; set; }
		public List<UbicacionLoteItemDto> Ubicaciones { get; set; } = new();
	}
}
