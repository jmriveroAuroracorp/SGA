using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGA_Desktop.Models
{
	/// <summary>
	/// Representa un resumen de un artículo con código y descripción,
	/// útil para listas maestras antes de mostrar el detalle de stock.
	/// </summary>
	public class ArticuloResumenDto
	{
		public string CodigoArticulo { get; set; } = string.Empty;

		public string DescripcionArticulo { get; set; } = string.Empty;

		public string Display => $"{CodigoArticulo} – {DescripcionArticulo}";
	}
}
