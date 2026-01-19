using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGA_Desktop.Models
{
	public class LogEvento
	{
		public DateTime Fecha { get; set; }
		public int IdUsuario { get; set; }
		public string Tipo { get; set; } = string.Empty;
		public string Origen { get; set; } = string.Empty;
		public string Descripcion { get; set; } = string.Empty;
		public string Detalle { get; set; } = string.Empty;
		public string IdDispositivo { get; set; } = string.Empty;
	}
}
