using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGA_Desktop.Models
{
	public class LoginResponse
	{
		public int operario { get; set; }
		public string nombreOperario { get; set; }
		public List<int> codigosAplicacion { get; set; }
		public List<string> codigosAlmacen { get; set; }
		public List<EmpresaDto> empresas { get; set; } = new();
		public string token { get; set; }
		public string codigoCentro { get; set; }

		public short? EmpresaPorDefecto { get; set; }   

	}

}
