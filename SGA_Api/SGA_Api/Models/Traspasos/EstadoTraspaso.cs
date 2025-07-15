using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SGA_Api.Models.Traspasos
{
	[Table("TipoEstadosTraspaso")]
	public class EstadoTraspaso
	{
		[Key]
		public string CodigoEstado { get; set; }
		public string Descripcion { get; set; }
	}


}
