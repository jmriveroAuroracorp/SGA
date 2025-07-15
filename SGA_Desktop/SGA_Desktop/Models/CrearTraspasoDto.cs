using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGA_Desktop.Models
{
	public class CrearTraspasoDto
	{
		public string AlmacenOrigen { get; set; }
		public string AlmacenDestino { get; set; }
		public Guid PaletId { get; set; }
		public int UsuarioInicioId { get; set; }
	}

}
