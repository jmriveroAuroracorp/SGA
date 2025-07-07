using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGA_Desktop.Models
{
	public class PaletCrearDto
	{
		public short CodigoEmpresa { get; set; }
		public int UsuarioAperturaId { get; set; }
		public string TipoPaletCodigo { get; set; } = "";
		public string? OrdenTrabajoId { get; set; }
	}
}
