using System;
using System.Globalization;
using System.Windows.Data;

namespace SGA_Desktop.Helpers
{
	public class EstaPendienteConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is string estado)
			{
				return string.Equals(estado, "PENDIENTE", StringComparison.OrdinalIgnoreCase);
			}
			return false;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
