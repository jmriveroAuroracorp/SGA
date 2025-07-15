using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGA_Desktop.Models
{
	public class FinalizarTraspasoDto
	{
		public string UbicacionDestino { get; set; } = null!;
		public int UsuarioFinalizacionId { get; set; }
		public DateTime FechaFinalizacion { get; set; }
	}
}
