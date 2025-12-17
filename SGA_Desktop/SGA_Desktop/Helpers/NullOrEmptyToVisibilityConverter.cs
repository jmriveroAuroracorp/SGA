using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows;

namespace SGA_Desktop.Helpers
{
	public class NullOrEmptyToVisibilityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			// Si la colección está vacía o null → oculto
			if (value == null)
				return Visibility.Collapsed;

			// Manejar DateTime? (Nullable<DateTime>)
			if (value is DateTime dateTime && dateTime != default(DateTime))
				return Visibility.Visible;

			if (value is IEnumerable enumerable)
			{
				foreach (var _ in enumerable)
					return Visibility.Visible; // tiene al menos un elemento
				return Visibility.Collapsed;
			}

			// Para strings y otros tipos, si no es null ni vacío, mostrar
			if (value is string str && !string.IsNullOrWhiteSpace(str))
				return Visibility.Visible;

			// Si es cualquier otro tipo no-null, mostrar
			return Visibility.Visible;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
