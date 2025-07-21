using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGA_Desktop.Models
{
	public class CerrarPaletMobilityDto

	{

		public int UsuarioId { get; set; }

		public string CodigoAlmacen { get; set; } = ""; // almacén origen

		public short CodigoEmpresa { get; set; }

		public string? UbicacionOrigen { get; set; } // ✅ OPCIONAL

	}

}
