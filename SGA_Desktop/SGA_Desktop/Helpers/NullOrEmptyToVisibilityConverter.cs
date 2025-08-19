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

			if (value is IEnumerable enumerable)
			{
				foreach (var _ in enumerable)
					return Visibility.Visible; // tiene al menos un elemento
				return Visibility.Collapsed;
			}

			return Visibility.Collapsed;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
