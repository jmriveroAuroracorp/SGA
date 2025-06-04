using SGA_Desktop.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGA_Desktop.Helpers
{
	public static class SessionManager
	{
		public static LoginResponse? UsuarioActual { get; set; }
	}

}
