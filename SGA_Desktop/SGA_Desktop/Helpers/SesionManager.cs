using SGA_Desktop.Models;
using System;

namespace SGA_Desktop.Helpers
{
	public static class SessionManager
	{
		public static LoginResponse? UsuarioActual { get; set; }
		public static string Token => UsuarioActual?.token ?? string.Empty;

		// Nuevo: ID numérico del operario
		public static int Operario => UsuarioActual?.operario ?? 0;

		// Nuevo: Nombre completo del operario
		public static string NombreOperario => UsuarioActual?.nombreOperario ?? string.Empty;

		public static short? EmpresaSeleccionada { get; private set; }

		public static event EventHandler? EmpresaCambiada;

		public static void SetEmpresa(short codigo)
		{
			EmpresaSeleccionada = codigo;
			EmpresaCambiada?.Invoke(null, EventArgs.Empty);   // avisa a quien escuche
		}
	}
}
