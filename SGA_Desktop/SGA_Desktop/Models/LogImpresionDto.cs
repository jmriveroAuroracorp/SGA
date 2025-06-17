using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGA_Desktop.Models
{
	public class LogImpresionDto
	{
		public string Usuario { get; set; } = string.Empty;
		public string Dispositivo { get; set; } = string.Empty;
		public DateTime RequestedAt { get; set; }
		public int? IdImpresora { get; set; }
		public int EtiquetaImpresa { get; set; }
		public int? Copias { get; set; }

		// Datos del artículo
		public string CodigoArticulo { get; set; } = string.Empty;
		public string DescripcionArticulo { get; set; } = string.Empty;
		public string CodigoAlternativo { get; set; } = string.Empty;
		public DateTime? FechaCaducidad { get; set; }
		public string Partida { get; set; } = string.Empty;
		public string Alergenos { get; set; } = string.Empty;

		// Ruta de la etiqueta (template)
		public string PathEtiqueta { get; set; } = string.Empty;
	}
}
