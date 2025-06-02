using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGA_Desktop.Models
{
	public class LoginRequest
	{
		public int operario { get; set; }
		public string contraseña { get; set; } = string.Empty;
	}
}
