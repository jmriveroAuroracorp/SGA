using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SGA_Desktop.Models
{
	public class EmpresaDto
	{
		[JsonPropertyName("codigo")]
		public short Codigo { get; set; }

		[JsonPropertyName("nombre")]
		public string Nombre { get; set; } = string.Empty;
		// NUEVA propiedad
		public string LogoPath => $"/Assets/Logos/{Codigo}.png";
	}
}
