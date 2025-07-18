using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGA_Desktop.Models
{
	public class CerrarPaletDto
	{
		public int UsuarioId { get; set; }
		public string CodigoAlmacen { get; set; } = "";           // almacén actual (origen), de las líneas
		public string CodigoAlmacenDestino { get; set; } = "";   // almacén destino, seleccionado por el usuario
		public string UbicacionDestino { get; set; } = "";       // ubicación dentro del destino
		public short CodigoEmpresa { get; set; }
	}


}
