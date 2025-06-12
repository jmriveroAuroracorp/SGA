using Microsoft.EntityFrameworkCore;

namespace SGA_Api.Models.Stock
{
	[Keyless]
	public class Articulo
	{
		public short CodigoEmpresa { get; set; }
		public string CodigoArticulo { get; set; } = null!;
		public string? DescripcionArticulo { get; set; }
		public string? CodigoAlternativo { get; set; }
		public string? CodigoAlternativo2 { get; set; }
		public string? ReferenciaEdi_ { get; set; }
		public string? MRHCodigoAlternativo3 { get; set; }
		public string? VCodigoDUN14 { get; set; }

	}
}
