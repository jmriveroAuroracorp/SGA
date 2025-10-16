using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGA_Desktop.Models
{
	public class UbicacionDto
	{
		[JsonProperty("codigoAlmacen")]
		public string CodigoAlmacen { get; set; } = "";

		[JsonProperty("ubicacion")]
		public string Ubicacion { get; set; } = "";

		public string UbicacionMostrada => string.IsNullOrWhiteSpace(Ubicacion) ? "SIN UBICAR" : Ubicacion;

		public override string ToString()
		{
			return UbicacionMostrada;
		}
	}
}
