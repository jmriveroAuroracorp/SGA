using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGA_Desktop.Models
{
	public class TraspasoDto
	{
		public Guid Id { get; set; }

		public string AlmacenOrigen { get; set; }
		public string AlmacenDestino { get; set; }

		public string CodigoEstado { get; set; }

		public DateTime FechaInicio { get; set; }
		public int UsuarioInicioId { get; set; }
		public string UsuarioInicioNombre { get; set; } = "";

		public Guid PaletId { get; set; }

		public DateTime? FechaFinalizacion { get; set; }
		public int? UsuarioFinalizacionId { get; set; }
		public string UsuarioFinalizacionNombre { get; set; } = "";
		public string? UbicacionDestino { get; set; }
		public string? CodigoPalet { get; set; }
		public string? TipoTraspaso { get; set; }
		public string? CodigoArticulo { get; set; }
		
		// Líneas del palet
		public List<LineaPaletDto> LineasPalet { get; set; } = new List<LineaPaletDto>();
		
		public string CodigoPrincipal
		{
			get
			{
				if (TipoTraspaso == "PALET" && !string.IsNullOrWhiteSpace(CodigoPalet))
					return CodigoPalet;
				if (TipoTraspaso == "ARTICULO" && !string.IsNullOrWhiteSpace(CodigoArticulo))
					return CodigoArticulo;
				return "(Sin código)";
			}
		}
		public string CodigoPrincipalAndEstado => $"{CodigoPrincipal} - {CodigoEstado}";


	}

}
