using SGA_Desktop.Models;
using System;
using System.Linq;

namespace SGA_Desktop.Helpers
{
	public static class SessionManager
	{
		public static LoginResponse? UsuarioActual { get; set; }
		public static string Token => UsuarioActual?.token ?? string.Empty;

		public static int Operario => UsuarioActual?.operario ?? 0;
		public static string NombreOperario => UsuarioActual?.nombreOperario ?? string.Empty;

		public static short? EmpresaSeleccionada { get; private set; }

		/// <summary>
		/// Devuelve el nombre de la empresa seleccionada, o cadena vacía si no hay ninguna.
		/// </summary>
		public static string EmpresaSeleccionadaNombre
			=> UsuarioActual?
				  .empresas?                              // colección de EmpresaDto
				  .FirstOrDefault(e => e.Codigo == EmpresaSeleccionada)?
				  .Nombre
			   ?? string.Empty;

		public static event EventHandler? EmpresaCambiada;

		public static void SetEmpresa(short codigo)
		{
			EmpresaSeleccionada = codigo;
			EmpresaCambiada?.Invoke(null, EventArgs.Empty);
		}
	}
}
