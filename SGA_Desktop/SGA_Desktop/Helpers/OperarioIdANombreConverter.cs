using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace SGA_Desktop.Helpers
{
	public class OperarioIdANombreConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is int id)
			{
				// Si solo tienes nombre del operario actual:
				if (id == SessionManager.Operario)
					return SessionManager.NombreOperario;

				// Si tuvieras un diccionario de usuarios en sesión, lo buscarías ahí:
				// return SessionManager.GetNombreOperario(id);

				// Como fallback, muestra el propio ID
				return id.ToString();
			}
			else if (value is string idString && int.TryParse(idString, out int parsedId))
			{
				// Manejar cuando el valor viene como string (como en UsuarioBloqueo)
				if (parsedId == SessionManager.Operario)
					return SessionManager.NombreOperario;

				// Como fallback, muestra el propio ID
				return idString;
			}
			return value?.ToString() ?? string.Empty;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			=> throw new NotImplementedException();
	}
}
