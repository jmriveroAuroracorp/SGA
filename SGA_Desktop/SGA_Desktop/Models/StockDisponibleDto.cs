using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGA_Desktop.Models
{
	public class StockDisponibleDto
	{
		public string CodigoAlmacen { get; set; }
		public string Ubicacion { get; set; }
		public string Partida { get; set; }
		public DateTime? FechaCaducidad { get; set; }
		public decimal UnidadSaldo { get; set; }

		public string CodigoArticulo { get; set; }
		public string DescripcionArticulo { get; set; }

		// editable
		public decimal CantidadAMover { get; set; }
	}
}
