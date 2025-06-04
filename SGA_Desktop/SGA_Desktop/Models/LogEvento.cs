using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGA_Desktop.Models
{
	public class LogEvento
	{
		public DateTime fecha { get; set; }
		public int idUsuario { get; set; }
		public string tipo { get; set; } = string.Empty;
		public string origen { get; set; } = string.Empty;
		public string descripcion { get; set; } = string.Empty;
		public string detalle { get; set; } = string.Empty;
		public string idDispositivo { get; set; } = string.Empty;
	}
}
