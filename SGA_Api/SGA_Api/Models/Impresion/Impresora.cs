using System.ComponentModel.DataAnnotations.Schema;

namespace SGA_Api.Models.Impresion
{
	[Table("impresoras")]
	public class Impresora
	{
		public int Id { get; set; }
		public string Nombre { get; set; } = null!;
	}
}
