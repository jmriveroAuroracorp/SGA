using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGA_Desktop.Models
{
	public class LineaPendienteDto
	{
		public string CodigoArticulo { get; set; }
		public string DescripcionArticulo { get; set; }
		public string Partida { get; set; }
		public string Ubicacion { get; set; }
		public decimal Cantidad { get; set; }
	}

}
