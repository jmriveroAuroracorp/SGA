using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGA_Desktop.Models
{
	public class LineaPaletDto
	{
		public string DescripcionArticulo { get; set; } = "";
		public int Cantidad { get; set; }
		public int UbicacionOrigenId { get; set; }
		// añade aquí cualquier otro campo que devuelva tu API (p.ej. CódigoArticulo, Partida, etc.)
	}
}
