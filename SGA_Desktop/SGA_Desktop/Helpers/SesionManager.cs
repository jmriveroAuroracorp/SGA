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
		public static string Token => UsuarioActual?.token ?? string.Empty;

		public static short? EmpresaSeleccionada { get; private set; }

		public static event EventHandler? EmpresaCambiada;

		public static void SetEmpresa(short codigo)
		{
			EmpresaSeleccionada = codigo;
			EmpresaCambiada?.Invoke(null, EventArgs.Empty);   // avisa a quien escuche
		}
	}
}


