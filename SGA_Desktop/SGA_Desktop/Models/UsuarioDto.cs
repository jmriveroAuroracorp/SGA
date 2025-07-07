using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGA_Desktop.Models
{
	public class UsuarioDto
	{
		public int IdUsuario { get; set; }
		public string? IdEmpresa { get; set; }
		public string? Impresora { get; set; }
		public string? Etiqueta { get; set; }
	}
}
