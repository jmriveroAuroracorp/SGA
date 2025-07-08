namespace SGA_Api.Models.Palet
{
	public class LogPalet
	{
		public int Id { get; set; }
		public Guid PaletId { get; set; }
		public DateTime Fecha { get; set; }
		public int IdUsuario { get; set; }
		public string Accion { get; set; } = null!;
		public string? Detalle { get; set; }

		// Opcional: navegación al palet
		public Palet? Palet { get; set; }
	}
}
