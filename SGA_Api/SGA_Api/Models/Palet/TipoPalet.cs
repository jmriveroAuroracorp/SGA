using Microsoft.EntityFrameworkCore;

namespace SGA_Api.Models.Palet
{
	[Keyless]
	public class TipoPalet
	{
		public string CodigoPalet { get; set; } = "";
		public string Descripcion { get; set; } = "";
	}

}
