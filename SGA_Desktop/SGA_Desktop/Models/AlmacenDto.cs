using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGA_Desktop.Models
{
	public class AlmacenDto
	{
		public string CodigoAlmacen { get; set; } = "";
		public string NombreAlmacen { get; set; } = "";
		public short CodigoEmpresa { get; set; }
		public bool EsDelCentro { get; set; }
		public string DescripcionCombo =>
	CodigoAlmacen == "Todas" ? "Todos" : $"{CodigoAlmacen} – {NombreAlmacen}";


	}

}
