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
	public class PaletsCountToVisibilityConverter : IValueConverter
	{
		// parámetro: "One" → visible solo si hay 1 palet
		// parámetro: "Many" → visible solo si hay >1
		// parámetro: "Any" → visible si hay al menos 1 palet
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is IEnumerable list)
			{
				int count = 0;
				foreach (var _ in list) count++;

				if ((string)parameter == "One")
					return count == 1 ? Visibility.Visible : Visibility.Collapsed;

				if ((string)parameter == "Many")
					return count > 1 ? Visibility.Visible : Visibility.Collapsed;

				if ((string)parameter == "Any")
					return count > 0 ? Visibility.Visible : Visibility.Collapsed;
			}
			return Visibility.Collapsed;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			=> throw new NotImplementedException();
	}
}
