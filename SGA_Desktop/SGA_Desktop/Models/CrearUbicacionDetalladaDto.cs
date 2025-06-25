using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGA_Desktop.Models
{
	public class CrearUbicacionDetalladaDto
	{
		public short CodigoEmpresa { get; set; }
		public string CodigoAlmacen { get; set; } = "";
		public string CodigoUbicacion { get; set; } = "";
		public string? DescripcionUbicacion { get; set; }

		public int? Pasillo { get; set; }
		public int? Estanteria { get; set; }
		public int? Altura { get; set; }
		public int? Posicion { get; set; }

		// CONFIGURACIÓN
		public int? TemperaturaMin { get; set; }
		public int? TemperaturaMax { get; set; }
		public string? TipoPaletPermitido { get; set; }
		public bool? Habilitada { get; set; } = true;
		public short? TipoUbicacionId { get; set; }
	}
}
