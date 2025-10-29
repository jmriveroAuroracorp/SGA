using System;
using System.Globalization;
using System.Windows.Data;

namespace SGA_Desktop.Helpers
{
	public class UbicacionConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
			{
				return "Sin Ubicar";
			}
			return value.ToString();
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
