using System.ComponentModel.DataAnnotations.Schema;

namespace SGA_Api.Models.Stock
{
	[Table("Vis_Articulos")]
	public class VisArticulo
	{
		public short CodigoEmpresa { get; set; }
		public string CodigoArticulo { get; set; } = null!;
		public string DescripcionArticulo { get; set; } = null!;
		public string CodigoAlternativo { get; set; } = null!;
		public string VNEWAlergenos { get; set; } = null!;  // concatenación de alérgenos
	}
}
