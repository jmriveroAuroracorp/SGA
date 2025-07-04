namespace SGA_Api.Models.Palet
{
	public class TipoEstadoPalet
	{
		public string CodigoEstado { get; set; } = null!;  // PK

		public string Descripcion { get; set; } = null!;
		public int Orden { get; set; }

		// Navegación opcional:
		public ICollection<Palet>? Palets { get; set; }
	}
}
