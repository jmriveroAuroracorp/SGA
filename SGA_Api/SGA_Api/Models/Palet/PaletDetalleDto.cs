namespace SGA_Api.Models.Palet
{
	public class PaletDetalleDto
	{
		public Guid PaletId { get; set; }          // Identificador único del palet
		public string CodigoPalet { get; set; } = null!;  // Ej: PAL25-0000111
		public string EstadoPalet { get; set; } = null!;  // Abierto, Cerrado, Vaciado
		public decimal Cantidad { get; set; }      // Cantidad en este palet
		public string? Ubicacion { get; set; }     
		public string? Partida { get; set; }      
		public DateTime? FechaApertura { get; set; } 
		public DateTime? FechaCierre { get; set; }    
	}
}
