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
		public string nombreOperario { get; set; } = string.Empty;
		public List<int> codigosAplicacion { get; set; } = new();
	}

}
