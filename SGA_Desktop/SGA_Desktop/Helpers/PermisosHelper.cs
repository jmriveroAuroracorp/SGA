using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGA_Desktop.Helpers
{
	public static class PermisosHelper
	{
		public static bool PuedeAccederA(int codigoAplicacion)
		{
			var usuario = SessionManager.UsuarioActual;
			return usuario?.codigosAplicacion?.Contains(codigoAplicacion) ?? false;
		}

		// Métodos específicos para cada funcionalidad
		public static bool PuedeAccederAPesaje() => PuedeAccederA(7);
		public static bool PuedeAccederAConsultaStock() => PuedeAccederA(10);
		public static bool PuedeAccederAImpresionEtiquetas() => PuedeAccederA(11);
		public static bool PuedeAccederATraspasos() => PuedeAccederA(12);
		public static bool PuedeAccederACalidad() => PuedeAccederA(16);
	}
}
